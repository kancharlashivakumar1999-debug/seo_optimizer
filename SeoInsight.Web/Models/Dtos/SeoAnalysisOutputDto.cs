namespace SeoInsight.Web.Models.Dtos;

public sealed class SeoAnalysisOutputDto
{
    public TargetAnalysisDto? TargetAnalysis { get; set; }
    public List<CompetitorDeepDiveDto> CompetitorDeepDive { get; set; } = [];
    public GapAnalysisDto? GapAnalysis { get; set; }
    public RecommendedModificationsDto? RecommendedModifications { get; set; }
    public RankPredictionDto? RankPrediction { get; set; }
}

public sealed class TargetAnalysisDto
{
    public string TargetUrl { get; set; } = string.Empty;
    public string TargetKeyword { get; set; } = string.Empty;
    public double CurrentPosition { get; set; }
    public string CurrentIntentFit { get; set; } = "Moderate"; // Poor | Moderate | High
    public string OverallDrawbackSummary { get; set; } = string.Empty;
}

public sealed class CompetitorDeepDiveDto
{
    public int Position { get; set; }
    public string CompetitorName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string WhyTheyAreAhead { get; set; } = string.Empty;
    public string ContentAngle { get; set; } = string.Empty;
    public string KeywordUsageAndIntent { get; set; } = string.Empty;
}

public sealed class GapAnalysisDto
{
    public List<string> OurMajorDrawbacks { get; set; } = [];
    public List<string> CompetitorAdvantages { get; set; } = [];
    public List<string> MissingContentTopics { get; set; } = [];
}

public sealed class RecommendedModificationsDto
{
    public TagRecommendationDto? TitleTag { get; set; }
    public TagRecommendationDto? MetaDescription { get; set; }
    public string H1 { get; set; } = string.Empty;
    public List<StructuralChangeDto> StructuralChanges { get; set; } = [];
    public List<ActionPlanItemDto> ActionPlan { get; set; } = [];
}

public sealed class TagRecommendationDto
{
    public string Recommended { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
}

public sealed class StructuralChangeDto
{
    public string Section { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public List<string> KeyElements { get; set; } = [];
}

public sealed class ActionPlanItemDto
{
    public int Priority { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Owner { get; set; } = "Content";
    public string Effort { get; set; } = "Medium";
    public string ExpectedImpact { get; set; } = "High";
}

public sealed class RankPredictionDto
{
    public double BaselinePosition { get; set; }
    public double ProjectedPosition { get; set; }
    public string ProjectedRange { get; set; } = string.Empty;
    public string TimelineWeeks { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public List<string> PrimaryRankingDrivers { get; set; } = [];
    public List<string> RisksOrDependencies { get; set; } = [];
}
