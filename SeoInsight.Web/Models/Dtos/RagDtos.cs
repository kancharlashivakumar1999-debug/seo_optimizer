namespace SeoInsight.Web.Models.Dtos;

public sealed class RunTaskRequest
{
    public string TaskName { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = [];
    public List<string> Locations { get; set; } = [];
    public string Note { get; set; } = string.Empty;
    public List<string> Implemented { get; set; } = [];
}

public sealed class RagInputDto
{
    public string Keyword { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string TargetWebsite { get; set; } = string.Empty;
    public int? OwnRankPosition { get; set; }
    public List<RagCompetitorItemDto> TopCompetitors { get; set; } = [];
    public List<string> PeopleAlsoAsk { get; set; } = [];
    public List<RagChunkDto> IngestedChunks { get; set; } = [];
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPrompt { get; set; } = string.Empty;
}

public sealed class RagCompetitorItemDto
{
    public int Position { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string WhyItRanks { get; set; } = string.Empty;
    public bool IsTargetSite { get; set; }
}

public sealed class RagChunkDto
{
    public string Source { get; set; } = string.Empty; // own | competitor
    public string Domain { get; set; } = string.Empty;
    public string Heading { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
}
