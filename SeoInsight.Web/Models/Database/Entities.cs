namespace SeoInsight.Web.Models.Database;

public sealed class AuditTaskEntity
{
    public Guid TaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string TargetWebsite { get; set; } = string.Empty;
    public string TargetKeyword { get; set; } = string.Empty;
    public string TargetLocation { get; set; } = string.Empty;
    public string TargetKeywordsJson { get; set; } = "[]";
    public string TargetLocationsJson { get; set; } = "[]";
    public string? PromptTemplate { get; set; }
    public string CheckEngine { get; set; } = "live-serp-rag";
    public int RepeatsPerRun { get; set; } = 1;
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "System";
    public string UpdatedBy { get; set; } = "System";
    public bool IsActive { get; set; } = true;
}

public sealed class TaskRunEntity
{
    public Guid RunId { get; set; }
    public Guid TaskId { get; set; }
    public string TargetKeyword { get; set; } = string.Empty;
    public string TargetLocation { get; set; } = string.Empty;
    public int RunNumber { get; set; } = 1;
    public string RunStatus { get; set; } = "Running";
    public string? RunNote { get; set; }
    public string ImplementedChangesJson { get; set; } = "[]";
    public int VisibilityScore { get; set; }
    public decimal? AverageRank { get; set; }
    public int TotalKeywordsChecked { get; set; }
    public int Top10Hits { get; set; }
    public string? Summary { get; set; }
    public decimal DurationSeconds { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "System";
    public string UpdatedBy { get; set; } = "System";
    public bool IsActive { get; set; } = true;
}

public sealed class SerpRequestDebugLogEntity
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Url { get; set; } = string.Empty;
    public int? Page { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class SerpCompetitorEntity
{
    public Guid CompetitorId { get; set; }
    public Guid RunId { get; set; }
    public Guid TaskId { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int Position { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Snippet { get; set; }
    public string? SerpHighlights { get; set; }
    public bool IsTargetSite { get; set; }
    public string? CrawledSummary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "System";
    public bool IsActive { get; set; } = true;
}

public sealed class RagInputPayloadEntity
{
    public Guid PayloadId { get; set; }
    public Guid RunId { get; set; }
    public Guid TaskId { get; set; }
    public string TargetKeyword { get; set; } = string.Empty;
    public string TargetLocation { get; set; } = string.Empty;
    public string TargetWebsite { get; set; } = string.Empty;
    public int? OwnRankPosition { get; set; }
    public string TopCompetitorsJson { get; set; } = "[]";
    public string PeopleAlsoAskJson { get; set; } = "[]";
    public string IngestedChunksJson { get; set; } = "[]";
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPrompt { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "System";
    public bool IsActive { get; set; } = true;
}

public sealed class ContentChunkEntity
{
    public Guid ChunkId { get; set; }
    public Guid RunId { get; set; }
    public Guid TaskId { get; set; }
    public string SourceType { get; set; } = "Competitor";
    public string SourceUrl { get; set; } = string.Empty;
    public int? CompetitorRank { get; set; }
    public string? HeadingHierarchy { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public int? TokenCount { get; set; }
    public float[]? Embedding { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "System";
    public bool IsActive { get; set; } = true;
}

public sealed class GapAnalysisResultEntity
{
    public Guid ResultId { get; set; }
    public Guid RunId { get; set; }
    public Guid TaskId { get; set; }
    public string LlmModel { get; set; } = "gemini-3.5-flash-lite";
    public string? TargetIntentFit { get; set; }
    public string? OverallDrawbackSummary { get; set; }
    public string TargetAnalysis { get; set; } = "{}";
    public string CompetitorDeepDive { get; set; } = "[]";
    public string GapAnalysis { get; set; } = "{}";
    public string RecommendedModifications { get; set; } = "{}";
    public string RankPrediction { get; set; } = "{}";
    public string? RawPrompt { get; set; }
    public string? RawResponse { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "System";
    public bool IsActive { get; set; } = true;
}
