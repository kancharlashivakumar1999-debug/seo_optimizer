using System.Text.Json;
using Microsoft.Extensions.Options;
using SeoInsight.Web.Constants;
using SeoInsight.Web.Infrastructure.Data;
using SeoInsight.Web.Models.Database;
using SeoInsight.Web.Models.Domain;
using SeoInsight.Web.Models.Options;
using SeoInsight.Web.Repositories.Interfaces;
using SeoInsight.Web.Services.Interfaces;

namespace SeoInsight.Web.Services.Implementations;

/// <summary>
/// Live SERP implementation using SerpApi.com.
/// </summary>
public sealed class SerpApiProvider : ISerpProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsSnapshot<SerpOptions> _options;
    private readonly IOptionsSnapshot<TrackingOptions> _trackingOptions;
    private readonly ISeoAuditRepository _auditRepository;
    private readonly ILogger<SerpApiProvider> _logger;

    public string ProviderName => ProviderConstants.SerpApi;

    public SerpApiProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsSnapshot<SerpOptions> options,
        IOptionsSnapshot<TrackingOptions> trackingOptions,
        ISeoAuditRepository auditRepository,
        ILogger<SerpApiProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _trackingOptions = trackingOptions;
        _auditRepository = auditRepository;
        _logger = logger;
    }

    public async Task<SerpResult> FetchSerpAsync(string keyword, string location, Guid runId, CancellationToken ct = default)
    {
        return await FetchSerpAsync(keyword, location, 1, runId, ct);
    }

    public async Task<SerpResult> FetchSerpAsync(string keyword, string location, int page, Guid runId, CancellationToken ct = default)
    {
        var opt = _options.Value;
        var result = new SerpResult
        {
            Keyword = keyword,
            Location = location,
            SearchEngine = opt.SearchEngine,
            FetchedAtUtc = DateTime.UtcNow
        };

        var apiKey = opt.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("SerpApi API Key is not configured in appsettings.json.");
        }

        var client = _httpClientFactory.CreateClient("SerpApiClient");

        int start = Math.Max(0, (page - 1));

        // Construct SerpApi Google search URL with deterministic parameters for stable, repeatable rankings:
        // - Removed 'no_cache=true' to allow SerpApi's consistency layer to serve identical rankings for the same query.
        // - Uses exact location anchoring (including Google UULE canonical encoding) to prevent proxy geo-drift.
        var queryParams = new List<KeyValuePair<string, string>>
        {
            new("engine", "google"),
            new("q", keyword)
        };

        if (!string.IsNullOrWhiteSpace(location))
        {
            queryParams.Add(new("location", location));
        }

        queryParams.Add(new("google_domain", "google.co.in"));
        queryParams.Add(new("gl", !string.IsNullOrWhiteSpace(opt.Country) ? opt.Country.ToLowerInvariant() : "in"));
        queryParams.Add(new("hl", !string.IsNullOrWhiteSpace(opt.Language) ? opt.Language : "en"));
        queryParams.Add(new("device", !string.IsNullOrWhiteSpace(opt.Device) ? opt.Device : "desktop"));
        queryParams.Add(new("start", start.ToString()));
        queryParams.Add(new("api_key", apiKey));

        // Format query string using application/x-www-form-urlencoded format ('+' for spaces)
        var queryString = string.Join("&", queryParams.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value).Replace("%20", "+")}"));

        var requestUrl = $"https://serpapi.com/search.json?{queryString}";
        result.RequestedUrl = requestUrl;

        // Persist exact requested URL directly from FetchSerpAsync into serp_request_debug_logs
        try
        {
            await _auditRepository.SaveSerpDebugLogAsync(new SerpRequestDebugLogEntity
            {
                Id = DbConstants.NewUuidV7(),
                RunId = runId, // Populated / associated with run
                Url = requestUrl,
                Page = page,
                CreatedAt = DateTime.UtcNow
            }, ct);
        }
        catch (Exception dbgEx)
        {
            _logger.LogWarning(dbgEx, "Could not write debug URL log in FetchSerpAsync");
        }

        _logger.LogInformation("Calling live SerpApi for query: '{Keyword}' in '{Location}'", keyword, location);

        var response = await client.GetAsync(requestUrl, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("SerpApi request failed with status {StatusCode}: {Error}", response.StatusCode, err);
            throw new HttpRequestException($"SerpApi request failed: HTTP {response.StatusCode} - {err}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 1. Extract Search Information / Total Results
        if (root.TryGetProperty("search_information", out var searchInfo))
        {
            if (searchInfo.TryGetProperty("total_results", out var tr) && tr.ValueKind == JsonValueKind.Number)
            {
                result.SearchVolume = tr.GetInt64();
            }
        }

        // 2. Extract People Also Ask
        if (root.TryGetProperty("related_questions", out var relatedQuestions))
        {
            foreach (var q in relatedQuestions.EnumerateArray())
            {
                if (q.TryGetProperty("question", out var questionProp))
                {
                    var qText = questionProp.GetString();
                    if (!string.IsNullOrWhiteSpace(qText))
                    {
                        result.PeopleAlsoAsk.Add(qText);
                    }
                }
            }
        }

        // 3. Extract Organic Results (Live top rankings)
        if (root.TryGetProperty("organic_results", out var organicResults))
        {
            int fallbackPos = start + 1;
            foreach (var item in organicResults.EnumerateArray())
            {
                var link = item.TryGetProperty("link", out var l) ? l.GetString() ?? "" : "";
                var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var snippet = item.TryGetProperty("snippet", out var s) ? s.GetString() ?? "" : "";
                
                int position = item.TryGetProperty("position", out var pos) && pos.ValueKind == JsonValueKind.Number
                    ? pos.GetInt32()
                    : fallbackPos++;

                // Ensure position reflects page offset if SerpApi returned 1-based index per page
                if (position <= 0 && page > 1)
                {
                    position += start;
                }

                string domain = "";
                if (item.TryGetProperty("displayed_link", out var dl))
                {
                    domain = dl.GetString() ?? "";
                }

                if (string.IsNullOrEmpty(domain) && Uri.TryCreate(link, UriKind.Absolute, out var uri))
                {
                    domain = uri.Host.Replace("www.", "");
                }

                bool isOwn = !string.IsNullOrEmpty(_trackingOptions.Value.OwnDomain) &&
                             domain.Contains(_trackingOptions.Value.OwnDomain, StringComparison.OrdinalIgnoreCase);

                result.OrganicResults.Add(new SerpItem
                {
                    Position = position,
                    Url = link,
                    Domain = domain,
                    Title = title,
                    Snippet = snippet,
                    IsOwnDomain = isOwn
                });
            }
        }

        _logger.LogInformation("SerpApi returned {Count} organic results and {PaaCount} PAA items for '{Keyword}'",
            result.OrganicResults.Count, result.PeopleAlsoAsk.Count, keyword);

        return result;
    }

    /// <summary>
    /// Generates a Google UULE (Universal Unique Location Identifier) parameter to lock Google Search
    /// results to exact canonical geographic coordinates, eliminating proxy IP location drift.
    /// </summary>
    private static string GenerateUule(string canonicalName)
    {
        if (string.IsNullOrWhiteSpace(canonicalName)) return string.Empty;
        var trimmed = canonicalName.Trim();
        const string base64Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        int len = trimmed.Length;
        if (len >= base64Alphabet.Length) return string.Empty;
        char lengthKey = base64Alphabet[len];
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(trimmed));
        return $"w+CAIQICI{lengthKey}{encoded}";
    }
}
