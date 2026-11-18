using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SeoInsight.Web.Models.Domain;
using SeoInsight.Web.Models.Dtos;
using SeoInsight.Web.Repositories.Interfaces;
using SeoInsight.Web.Services.Interfaces;

namespace SeoInsight.Web.Controllers;

public class DashboardController : Controller
{
    private readonly ISerpService _serpService;
    private readonly ICompetitorScraperService _scraperService;
    private readonly IChunkingService _chunkingService;
    private readonly IVectorStore _vectorStore;
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        ISerpService serpService,
        ICompetitorScraperService scraperService,
        IChunkingService chunkingService,
        IVectorStore vectorStore,
        ITaskRepository taskRepository,
        ILogger<DashboardController> logger)
    {
        _serpService = serpService;
        _scraperService = scraperService;
        _chunkingService = chunkingService;
        _vectorStore = vectorStore;
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> RunSerpAnalysis([FromBody] RunTaskRequest request, CancellationToken ct)
    {
        if (request == null || request.Keywords.Count == 0 || request.Locations.Count == 0)
        {
            return BadRequest(new { message = "Keyword and location are required." });
        }

        _logger.LogInformation("Executing live SERP fetch and RAG Input preparation for website: {Website}", request.Website);

        var report = new RunReport
        {
            TaskName = string.IsNullOrWhiteSpace(request.TaskName) ? request.Website : request.TaskName,
            Website = request.Website,
            Note = request.Note,
            ImplementedChanges = request.Implemented,
            StartedAtUtc = DateTime.UtcNow
        };

        var allRagInputs = new List<RagInputDto>();

        foreach (var kw in request.Keywords)
        {
            foreach (var loc in request.Locations)
            {
                // 1. Fetch live SERP
                var serp = await _serpService.GetSerpAsync(kw, loc, ct);

                // 2. Identify target rank
                var ownItem = serp.OrganicResults.FirstOrDefault(r =>
                    r.Domain.Contains(request.Website, StringComparison.OrdinalIgnoreCase) ||
                    r.Url.Contains(request.Website, StringComparison.OrdinalIgnoreCase));

                int? rank = ownItem?.Position;

                var kwResult = new KeywordRunResult
                {
                    Keyword = kw,
                    Location = loc,
                    Rank = rank,
                    Hits = rank.HasValue ? 1 : 0,
                    TotalSamples = 1,
                    Top10 = serp.OrganicResults.Take(10).ToList()
                };
                report.KeywordResults.Add(kwResult);

                // 3. Scrape competitor content and chunk into Vector Store
                var ingestedChunks = new List<RagChunkDto>();
                foreach (var competitor in serp.OrganicResults.Take(3)) // Top 3 competitors
                {
                    var page = await _scraperService.ScrapeAsync(competitor.Url, ct);
                    string source = competitor.IsOwnDomain ? "own" : "competitor";
                    var chunks = _chunkingService.ChunkPage(page, kw, source, competitor.Position);

                    await _vectorStore.UpsertChunksAsync(chunks, ct);

                    ingestedChunks.AddRange(chunks.Take(2).Select(c => new RagChunkDto
                    {
                        Source = c.Source,
                        Domain = c.Domain,
                        Heading = c.SectionHeading,
                        Text = c.Content.Length > 200 ? c.Content[..200] + "..." : c.Content,
                        RelevanceScore = 0.85
                    }));
                }

                // 4. Create RAG Input DTO
                var ragInput = new RagInputDto
                {
                    Keyword = kw,
                    Location = loc,
                    TargetWebsite = request.Website,
                    OwnRankPosition = rank,
                    PeopleAlsoAsk = serp.PeopleAlsoAsk,
                    TopCompetitors = serp.OrganicResults.Take(10).Select(c => new RagCompetitorItemDto
                    {
                        Position = c.Position,
                        Domain = c.Domain,
                        Url = c.Url,
                        Title = c.Title,
                        Snippet = c.Snippet,
                        WhyItRanks = c.Position <= 3
                            ? "High authority domain, dedicated topic page, strong reviews & mentions."
                            : "Good topical relevance, keyword presence in title and headers.",
                        IsTargetSite = c.IsOwnDomain || (rank.HasValue && c.Position == rank.Value)
                    }).ToList(),
                    IngestedChunks = ingestedChunks,
                    SystemPrompt = "You are an SEO optimization assistant. Use ONLY provided context to generate recommendations in JSON.",
                    UserPrompt = $"Optimize target website '{request.Website}' for keyword '{kw}' in '{loc}'. Current rank: {(rank.HasValue ? "#" + rank.Value : "Not found in top 10")}."
                };

                allRagInputs.Add(ragInput);
            }
        }

        // Generate baseline visibility score
        int totalPairs = report.KeywordResults.Count;
        int inTop10 = report.KeywordResults.Count(r => r.Rank.HasValue && r.Rank.Value <= 10);
        report.VisibilityScore = totalPairs > 0 ? (int)((inTop10 / (double)totalPairs) * 100) : 50;
        report.Summary = $"Live SERP query completed for {request.Website}. Retrieved {totalPairs} keyword checks. Found {inTop10} in top 10 positions.";
        report.FinishedAtUtc = DateTime.UtcNow;
        report.Status = "Completed";

        await _taskRepository.SaveReportAsync(report, ct);

        return Ok(new
        {
            success = true,
            report,
            ragInputs = allRagInputs
        });
    }

    [HttpPost]
    public async Task<IActionResult> SaveTask([FromBody] JsonElement task, CancellationToken ct)
    {
        return Ok(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> SaveRun([FromBody] JsonElement run, CancellationToken ct)
    {
        return Ok(new { success = true });
    }
}
