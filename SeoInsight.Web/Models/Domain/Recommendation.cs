namespace SeoInsight.Web.Models.Domain;

/// <summary>
/// Domain model for AI-generated SEO recommendations.
/// </summary>
public sealed class Recommendation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Keyword { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string Diagnosis { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium"; // High, Medium, Low
    public List<string> NewTitleOptions { get; set; } = [];
    public string NewMetaDescription { get; set; } = string.Empty;
    public List<string> MissingTopicsHeadings { get; set; } = [];
    public List<FaqItem> FaqToAdd { get; set; } = [];
    public List<string> InternalLinkSuggestions { get; set; } = [];
    public List<RewriteSection> RewriteSections { get; set; } = [];
    public string ExpectedImpact { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool Done { get; set; }
}

public sealed class FaqItem
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

public sealed class RewriteSection
{
    public string Heading { get; set; } = string.Empty;
    public string Draft { get; set; } = string.Empty;
}
