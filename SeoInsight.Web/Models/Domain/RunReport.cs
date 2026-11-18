namespace SeoInsight.Web.Models.Domain;

/// <summary>
/// Domain model for an orchestrated background analysis run report.
/// </summary>
public sealed class RunReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskName { get; set; } = "Default SEO Audit";
    public string Website { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAtUtc { get; set; }
    public string Status { get; set; } = "Running"; // Pending, Running, Completed, Failed
    public string? Note { get; set; }
    public List<string> ImplementedChanges { get; set; } = [];
    public int VisibilityScore { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string ChangeImpact { get; set; } = string.Empty;
    public List<string> Wins { get; set; } = [];
    public List<string> Issues { get; set; } = [];
    public List<string> Patterns { get; set; } = [];
    public List<CompetitorLeader> Competitors { get; set; } = [];
    public List<KeywordRunResult> KeywordResults { get; set; } = [];
    public List<Recommendation> Recommendations { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public int PromptTokensUsed { get; set; }
    public int CompletionTokensUsed { get; set; }
    public double DurationSeconds => FinishedAtUtc.HasValue ? (FinishedAtUtc.Value - StartedAtUtc).TotalSeconds : 0;
}

public sealed class CompetitorLeader
{
    public string Domain { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string WhyTop { get; set; } = string.Empty;
    public int Appearances { get; set; }
    public double AveragePosition { get; set; }
    public int BestPosition { get; set; }
}

public sealed class KeywordRunResult
{
    public string Keyword { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public double? Rank { get; set; }
    public double? PreviousRank { get; set; }
    public int Hits { get; set; }
    public int TotalSamples { get; set; } = 3;
    public List<SerpItem> Top10 { get; set; } = [];
    public string? ReasonWhy { get; set; }
}
