using System.Text.Json;
using Microsoft.Extensions.Options;
using SeoInsight.Web.Constants;
using SeoInsight.Web.Models.Domain;
using SeoInsight.Web.Models.Options;
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
    private readonly ILogger<SerpApiProvider> _logger;

    public string ProviderName => ProviderConstants.SerpApi;

    public SerpApiProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsSnapshot<SerpOptions> options,
        IOptionsSnapshot<TrackingOptions> trackingOptions,
        ILogger<SerpApiProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _trackingOptions = trackingOptions;
        _logger = logger;
    }

    public async Task<SerpResult> FetchSerpAsync(string keyword, string location, CancellationToken ct = default)
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

        // Construct live SerpApi Google search URL
        var queryParams = new Dictionary<string, string>
        {
            ["engine"] = "google",
            ["q"] = keyword,
            ["api_key"] = apiKey,
            ["num"] = opt.ResultsDepth.ToString(),
            ["device"] = opt.Device
        };

        if (!string.IsNullOrWhiteSpace(location))
        {
            queryParams["location"] = location;
        }

        var queryString = string.Join("&", queryParams.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        var requestUrl = $"https://serpapi.com/search.json?{queryString}";

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
            int fallbackPos = 1;
            foreach (var item in organicResults.EnumerateArray())
            {
                var link = item.TryGetProperty("link", out var l) ? l.GetString() ?? "" : "";
                var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var snippet = item.TryGetProperty("snippet", out var s) ? s.GetString() ?? "" : "";
                
                int position = item.TryGetProperty("position", out var pos) && pos.ValueKind == JsonValueKind.Number
                    ? pos.GetInt32()
                    : fallbackPos++;

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
}
