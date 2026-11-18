using System.ComponentModel.DataAnnotations;

namespace SeoInsight.Web.Models.Options;

/// <summary>
/// Strongly-typed options for the SERP provider configuration.
/// </summary>
public sealed class SerpOptions
{
    public const string SectionName = "Serp";

    [Required]
    public string Provider { get; set; } = "DataForSEO";

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Login { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    [Required]
    public string SearchEngine { get; set; } = "google";

    [Required]
    public string Country { get; set; } = "IN";

    [Required]
    public string Language { get; set; } = "en";

    [Required]
    public string Device { get; set; } = "desktop";

    [Range(1, 100)]
    public int ResultsDepth { get; set; } = 10;

    [Range(5, 120)]
    public int TimeoutSeconds { get; set; } = 30;

    [Range(0, 5)]
    public int RetryCount { get; set; } = 3;

    [Range(1, 10)]
    public int MaxConcurrentRequests { get; set; } = 3;
}
