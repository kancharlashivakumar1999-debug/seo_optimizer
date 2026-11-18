using System.ComponentModel.DataAnnotations;

namespace SeoInsight.Web.Models.Options;

/// <summary>
/// Strongly-typed options for web scraper.
/// </summary>
public sealed class ScraperOptions
{
    public const string SectionName = "Scraper";

    [Required]
    public string UserAgent { get; set; } = "SeoInsightCrawler/1.0";

    public bool RespectRobotsTxt { get; set; } = true;

    [Range(5, 60)]
    public int TimeoutSeconds { get; set; } = 20;

    [Range(1000, 10000000)]
    public int MaxContentLength { get; set; } = 500000;
}
