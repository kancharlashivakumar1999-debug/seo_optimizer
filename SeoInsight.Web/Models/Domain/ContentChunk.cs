namespace SeoInsight.Web.Models.Domain;

/// <summary>
/// Domain model for an ingested text chunk with embedding vector and metadata.
/// </summary>
public sealed class ContentChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Url { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Keyword { get; set; } = string.Empty;
    public string Source { get; set; } = "competitor"; // "own" | "competitor"
    public string SectionHeading { get; set; } = string.Empty;
    public int RankPosition { get; set; }
    public DateTime FetchedDate { get; set; } = DateTime.UtcNow;
    public string Content { get; set; } = string.Empty;
    public int TokenEstimate { get; set; }
    public float[]? Embedding { get; set; }
    public double Score { get; set; }
}
