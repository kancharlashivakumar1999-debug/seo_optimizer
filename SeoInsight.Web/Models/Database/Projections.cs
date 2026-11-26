namespace SeoInsight.Web.Models.Database;

public sealed class TaskSummaryProjection
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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "System";
    public string UpdatedBy { get; set; } = "System";
    public int TotalRuns { get; set; }
    public decimal? LatestAverageRank { get; set; }
    public decimal? PreviousAverageRank { get; set; }
    public DateTime? LastRunAt { get; set; }
    public List<decimal> RecentRanks { get; set; } = new();
}

public sealed class DashboardMetricsProjection
{
    public decimal? AvgRank { get; set; }
    public decimal? AvgRankDelta { get; set; }
    public int Top10Count { get; set; }
    public int TotalKeywords { get; set; }
    public int ImprovedCount { get; set; }
    public int DroppedCount { get; set; }
    public int UnchangedCount { get; set; }
    public int NotRankingCount { get; set; }
}

public sealed class BiggestMoveProjection
{
    public string TaskName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Keyword { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int PreviousRank { get; set; }
    public int CurrentRank { get; set; }
    public int Delta { get; set; }
}

public sealed class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public sealed class TaskRunSummaryProjection
{
    public Guid RunId { get; set; }
    public Guid TaskId { get; set; }
    public int RunNumber { get; set; }
    public string RunStatus { get; set; } = "Completed";
    public string? RunNote { get; set; }
    public string ImplementedChangesJson { get; set; } = "[]";
    public int VisibilityScore { get; set; }
    public decimal? AverageRank { get; set; }
    public int TotalKeywordsChecked { get; set; }
    public int Top10Hits { get; set; }
    public string? Summary { get; set; }
    public decimal DurationSeconds { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool HasGapAnalysis { get; set; }
}

public sealed class CompetitorItemProjection
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
    public DateTime CreatedAt { get; set; }
}

public sealed class ContentChunkProjection
{
    public Guid ChunkId { get; set; }
    public Guid RunId { get; set; }
    public Guid TaskId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public int? CompetitorRank { get; set; }
    public string? HeadingHierarchy { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public int? TokenCount { get; set; }
    public float[]? Embedding { get; set; }
}

public sealed class GapAnalysisProjection
{
    public Guid ResultId { get; set; }
    public Guid RunId { get; set; }
    public Guid TaskId { get; set; }
    public string LlmModel { get; set; } = string.Empty;
    public string? TargetIntentFit { get; set; }
    public string? OverallDrawbackSummary { get; set; }
    public string TargetAnalysisJson { get; set; } = "{}";
    public string CompetitorDeepDiveJson { get; set; } = "[]";
    public string GapAnalysisJson { get; set; } = "{}";
    public string RecommendedModificationsJson { get; set; } = "{}";
    public string RankPredictionJson { get; set; } = "{}";
    public string? RawPrompt { get; set; }
    public string? RawResponse { get; set; }
    public DateTime CreatedAt { get; set; }
}
