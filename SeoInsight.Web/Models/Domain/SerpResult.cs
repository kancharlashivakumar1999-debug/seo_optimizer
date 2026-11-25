namespace SeoInsight.Web.Models.Domain;

/// <summary>
/// Domain model for an organic SERP result item.
/// </summary>
public sealed class SerpItem
{
    public int Position { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public bool IsOwnDomain { get; set; }
}

/// <summary>
/// Domain model for SERP feature items like People Also Ask, featured snippet, video, etc.
/// </summary>
public sealed class SerpFeature
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Normalized domain model representing SERP query output.
/// </summary>
public sealed class SerpResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Keyword { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string SearchEngine { get; set; } = "google";
    public string? RequestedUrl { get; set; }
    public DateTime FetchedAtUtc { get; set; } = DateTime.UtcNow;
    public long? SearchVolume { get; set; }
    public double? KeywordDifficulty { get; set; }
    public List<SerpItem> OrganicResults { get; set; } = [];
    public List<SerpFeature> Features { get; set; } = [];
    public List<string> PeopleAlsoAsk { get; set; } = [];
}
