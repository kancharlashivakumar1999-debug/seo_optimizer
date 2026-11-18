namespace SeoInsight.Web.Models.Domain;

/// <summary>
/// Domain model for an extracted competitor or own web page.
/// </summary>
public sealed class CompetitorPage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Url { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string MetaDescription { get; set; } = string.Empty;
    public List<HeadingNode> Headings { get; set; } = [];
    public string CleanedBodyText { get; set; } = string.Empty;
    public int WordCount { get; set; }
    public List<string> SchemaTypes { get; set; } = [];
    public List<FaqBlock> Faqs { get; set; } = [];
    public DateTime ScrapedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsSuccess { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

public sealed class HeadingNode
{
    public string Level { get; set; } = "H2"; // H1, H2, H3
    public string Text { get; set; } = string.Empty;
}

public sealed class FaqBlock
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}
