using System.ComponentModel.DataAnnotations;

namespace SeoInsight.Web.Models.Options;

/// <summary>
/// Strongly-typed options for tracked domain and target keywords.
/// </summary>
public sealed class TrackingOptions
{
    public const string SectionName = "Tracking";

    public string OwnDomain { get; set; } = string.Empty;

    public List<KeywordTarget> Keywords { get; set; } = [];

    public List<string> CompetitorDomains { get; set; } = [];
}

public sealed class KeywordTarget
{
    [Required]
    public string Keyword { get; set; } = string.Empty;

    public string TargetUrl { get; set; } = string.Empty;
}
