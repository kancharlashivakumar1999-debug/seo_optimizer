using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using SeoInsight.Web.Models.Domain;
using SeoInsight.Web.Models.Options;
using SeoInsight.Web.Services.Interfaces;

namespace SeoInsight.Web.Services.Implementations;

public sealed partial class CompetitorScraperService : ICompetitorScraperService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<ScraperOptions> _scraperOptions;
    private readonly ILogger<CompetitorScraperService> _logger;

    public CompetitorScraperService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<ScraperOptions> scraperOptions,
        ILogger<CompetitorScraperService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _scraperOptions = scraperOptions;
        _logger = logger;
    }

    public async Task<CompetitorPage> ScrapeAsync(string url, CancellationToken ct = default)
    {
        var domain = GetDomain(url);
        var page = new CompetitorPage
        {
            Url = url,
            Domain = domain,
            ScrapedAtUtc = DateTime.UtcNow
        };

        try
        {
            var client = _httpClientFactory.CreateClient("ScraperClient");
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                page.IsSuccess = false;
                page.ErrorMessage = $"HTTP {response.StatusCode}";
                return page;
            }

            var html = await response.Content.ReadAsStringAsync(ct);
            ParseHtml(page, html);
            return page;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scrape {Url}. Creating fallback competitor page context.", url);
            page.IsSuccess = false;
            page.ErrorMessage = ex.Message;
            PopulateFallback(page);
            return page;
        }
    }

    private static void ParseHtml(CompetitorPage page, string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Remove script, style, nav, footer, noscript
        var unwanted = doc.DocumentNode.SelectNodes("//script|//style|//nav|//footer|//noscript|//header|//svg");
        if (unwanted != null)
        {
            foreach (var node in unwanted)
            {
                node.Remove();
            }
        }

        // Title
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        page.Title = titleNode != null ? WebUtility.HtmlDecode(titleNode.InnerText).Trim() : string.Empty;

        // Meta Description
        var metaDesc = doc.DocumentNode.SelectSingleNode("//meta[@name='description']/@content")
                       ?? doc.DocumentNode.SelectSingleNode("//meta[@property='og:description']/@content");
        page.MetaDescription = metaDesc != null ? WebUtility.HtmlDecode(metaDesc.GetAttributeValue("content", "")).Trim() : string.Empty;

        // Headings H1, H2, H3
        var headingNodes = doc.DocumentNode.SelectNodes("//h1|//h2|//h3");
        if (headingNodes != null)
        {
            foreach (var hn in headingNodes)
            {
                var text = WebUtility.HtmlDecode(hn.InnerText).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    page.Headings.Add(new HeadingNode
                    {
                        Level = hn.Name.ToUpperInvariant(),
                        Text = text
                    });
                }
            }
        }

        // Body text
        var bodyNode = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
        var rawText = WebUtility.HtmlDecode(bodyNode.InnerText);
        var cleaned = WhitespaceRegex().Replace(rawText, " ").Trim();
        page.CleanedBodyText = cleaned;
        page.WordCount = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        // Schema Types
        var schemaScripts = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
        if (schemaScripts != null)
        {
            foreach (var s in schemaScripts)
            {
                var match = SchemaTypeRegex().Match(s.InnerText);
                if (match.Success)
                {
                    page.SchemaTypes.Add(match.Groups[1].Value);
                }
            }
        }
    }

    private static void PopulateFallback(CompetitorPage page)
    {
        page.Title = $"{page.Domain} - Services & Specialized Solutions";
        page.MetaDescription = $"Expert solutions and comprehensive service breakdown for {page.Domain}.";
        page.Headings =
        [
            new HeadingNode { Level = "H1", Text = $"{page.Domain} Overview & Quality Care" },
            new HeadingNode { Level = "H2", Text = "Comprehensive Service Packages & Procedures" },
            new HeadingNode { Level = "H2", Text = "Patient Reviews, Technology & Pricing Information" },
            new HeadingNode { Level = "H3", Text = "Frequently Asked Questions" }
        ];
        page.CleanedBodyText = $"{page.Title}. Comprehensive service offering with transparent pricing, certified practitioners, verified reviews, and modern clinical facilities. Detailed treatment workflows and patient guarantees provided.";
        page.WordCount = page.CleanedBodyText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static string GetDomain(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.Host.Replace("www.", "");
        }
        return url;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"""@type""\s*:\s*""([^""]+)""")]
    private static partial Regex SchemaTypeRegex();
}
