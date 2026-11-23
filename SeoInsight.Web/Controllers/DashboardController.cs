using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SeoInsight.Web.Infrastructure.Data;
using SeoInsight.Web.Models.Database;
using SeoInsight.Web.Models.Domain;
using SeoInsight.Web.Models.Dtos;
using SeoInsight.Web.Repositories.Interfaces;
using SeoInsight.Web.Services.Implementations;
using SeoInsight.Web.Services.Interfaces;

namespace SeoInsight.Web.Controllers;

public class DashboardController : Controller
{
    private readonly ISerpService _serpService;
    private readonly ICompetitorScraperService _scraperService;
    private readonly IChunkingService _chunkingService;
    private readonly IVectorStore _vectorStore;
    private readonly ISeoAuditRepository _auditRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IRagAnalysisService _ragAnalysisService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        ISerpService serpService,
        ICompetitorScraperService scraperService,
        IChunkingService chunkingService,
        IVectorStore vectorStore,
        ISeoAuditRepository auditRepository,
        ITaskRepository taskRepository,
        IRagAnalysisService ragAnalysisService,
        ILogger<DashboardController> logger)
    {
        _serpService = serpService;
        _scraperService = scraperService;
        _chunkingService = chunkingService;
        _vectorStore = vectorStore;
        _auditRepository = auditRepository;
        _taskRepository = taskRepository;
        _ragAnalysisService = ragAnalysisService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboardState(CancellationToken ct)
    {
        var tasks = await _auditRepository.GetAllTasksAsync(ct);
        var reports = await _taskRepository.GetAllReportsAsync(ct);
        return Ok(new { success = true, tasks, reports });
    }

    [HttpGet]
    public async Task<IActionResult> GetTaskDetail(Guid id, CancellationToken ct)
    {
        var task = await _auditRepository.GetTaskByIdAsync(id, ct);
        if (task == null) return NotFound(new { message = "Task not found." });

        var runs = await _auditRepository.GetRunsByTaskIdAsync(id, ct);

        return Ok(new
        {
            success = true,
            task,
            runs
        });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteTask(Guid id, CancellationToken ct)
    {
        var deleted = await _auditRepository.DeleteTaskAsync(id, ct);
        return Ok(new { success = deleted });
    }

    [HttpPost]
    public async Task<IActionResult> RunSerpAnalysis([FromBody] RunTaskRequest request, CancellationToken ct)
    {
        if (request == null || request.Keywords.Count == 0 || request.Locations.Count == 0)
        {
            return BadRequest(new { message = "Keyword and location are required." });
        }

        _logger.LogInformation("Executing live SERP fetch and RAG Input preparation for website: {Website}", request.Website);

        // Ensure task exists in DB or find matching task
        Guid taskId = Guid.Empty;
        if (!string.IsNullOrWhiteSpace(request.TaskId) && Guid.TryParse(request.TaskId, out var parsedTaskId))
        {
            taskId = parsedTaskId;
            var existingTask = await _auditRepository.GetTaskByIdAsync(taskId, ct);
            if (existingTask == null)
            {
                var newTask = new AuditTaskEntity
                {
                    TaskId = taskId,
                    TaskName = string.IsNullOrWhiteSpace(request.TaskName) ? request.Website : request.TaskName,
                    TargetWebsite = request.Website,
                    TargetKeywordsJson = JsonSerializer.Serialize(request.Keywords),
                    TargetLocationsJson = JsonSerializer.Serialize(request.Locations),
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _auditRepository.SaveTaskAsync(newTask, ct);
            }
        }
        else
        {
            var allTasks = await _auditRepository.GetAllTasksAsync(ct);
            var existingTask = allTasks.FirstOrDefault(t =>
                t.TargetWebsite.Equals(request.Website, StringComparison.OrdinalIgnoreCase) ||
                t.TaskName.Equals(request.TaskName, StringComparison.OrdinalIgnoreCase));

            taskId = existingTask?.TaskId ?? DbConstants.NewUuidV7();
            if (existingTask == null)
            {
                var newTask = new AuditTaskEntity
                {
                    TaskId = taskId,
                    TaskName = string.IsNullOrWhiteSpace(request.TaskName) ? request.Website : request.TaskName,
                    TargetWebsite = request.Website,
                    TargetKeywordsJson = JsonSerializer.Serialize(request.Keywords),
                    TargetLocationsJson = JsonSerializer.Serialize(request.Locations),
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _auditRepository.SaveTaskAsync(newTask, ct);
            }
        }

        // Generate isolated execution run_id (UUIDv7)
        Guid runId = DbConstants.NewUuidV7();
        var startTime = DateTime.UtcNow;

        var report = new RunReport
        {
            Id = runId.ToString(),
            TaskName = string.IsNullOrWhiteSpace(request.TaskName) ? request.Website : request.TaskName,
            Website = request.Website,
            Note = request.Note,
            ImplementedChanges = request.Implemented,
            StartedAtUtc = startTime
        };

        // Persist execution run mapping to task_runs table FIRST before child records
        await _auditRepository.SaveTaskRunAsync(new TaskRunEntity
        {
            RunId = runId,
            TaskId = taskId,
            RunNumber = 1,
            RunStatus = "Running",
            RunNote = request.Note,
            ImplementedChangesJson = JsonSerializer.Serialize(request.Implemented),
            VisibilityScore = 0,
            AverageRank = null,
            TotalKeywordsChecked = request.Keywords.Count * request.Locations.Count,
            Top10Hits = 0,
            Summary = "Live SERP & RAG processing in progress",
            DurationSeconds = 0,
            StartedAt = startTime,
            CreatedAt = startTime,
            UpdatedAt = startTime
        }, ct);

        var allRagInputs = new List<RagInputDto>();
        var competitorsToSave = new List<SerpCompetitorEntity>();

        foreach (var kw in request.Keywords)
        {
            foreach (var loc in request.Locations)
            {
                // 1. Fetch live SERP with pagination loop up to 5 pages
                // Normalize target website/domain for matching
                string normalizedTarget = request.Website.Trim().ToLowerInvariant()
                    .Replace("https://", "")
                    .Replace("http://", "")
                    .Replace("www.", "")
                    .TrimEnd('/');

                SerpResult serp = null!;
                SerpItem? ownItem = null;
                var allOrganicResults = new List<SerpItem>();
                var allPaa = new List<string>();

                for (int page = 1; page <= 5; page++)
                {
                    var pageSerp = await _serpService.GetSerpAsync(kw, loc, page, ct);
                    if (serp == null)
                    {
                        serp = pageSerp;
                    }

                    if (pageSerp.PeopleAlsoAsk.Count > 0)
                    {
                        foreach (var paa in pageSerp.PeopleAlsoAsk)
                        {
                            if (!allPaa.Contains(paa)) allPaa.Add(paa);
                        }
                    }

                    foreach (var item in pageSerp.OrganicResults)
                    {
                        string itemDomain = (item.Domain ?? "").Trim().ToLowerInvariant().Replace("www.", "");
                        string itemUrl = (item.Url ?? "").Trim().ToLowerInvariant();

                        bool isMatch = (!string.IsNullOrEmpty(normalizedTarget) &&
                                       (itemDomain.Contains(normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
                                        itemUrl.Contains(normalizedTarget, StringComparison.OrdinalIgnoreCase)));

                        if (isMatch)
                        {
                            item.IsOwnDomain = true;
                            if (ownItem == null)
                            {
                                ownItem = item;
                            }
                        }

                        allOrganicResults.Add(item);
                    }

                    // Check if target website is found in this page
                    if (ownItem != null)
                    {
                        _logger.LogInformation("Found target website '{Target}' at position {Pos} on SERP page {Page} for keyword '{Keyword}'",
                            request.Website, ownItem.Position, page, kw);
                        break;
                    }

                    // If fewer results returned than requested depth, reached end of SERP
                    if (pageSerp.OrganicResults.Count == 0)
                    {
                        break;
                    }
                }

                // Attach consolidated organic results and PAA
                serp.OrganicResults = allOrganicResults;
                serp.PeopleAlsoAsk = allPaa;

                // 2. Identify target rank
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

                // 3. Add competitors linked to runId (up to top 10 unique positions from traversed pages)
                foreach (var comp in serp.OrganicResults.OrderBy(c => c.Position).Take(10))
                {
                    competitorsToSave.Add(new SerpCompetitorEntity
                    {
                        CompetitorId = DbConstants.NewUuidV7(),
                        RunId = runId,
                        TaskId = taskId,
                        Keyword = kw,
                        Location = loc,
                        Position = comp.Position,
                        Domain = comp.Domain,
                        Title = comp.Title,
                        Url = comp.Url,
                        Snippet = comp.Snippet,
                        SerpHighlights = comp.Snippet,
                        IsTargetSite = comp.IsOwnDomain,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // 4. Scrape competitor content and chunk into Vector Store
                var ingestedChunks = new List<RagChunkDto>();
                foreach (var competitor in serp.OrganicResults.OrderBy(c => c.Position).Take(3)) // Top 3 competitors
                {
                    var page = await _scraperService.ScrapeAsync(competitor.Url, ct);
                    string source = competitor.IsOwnDomain ? "own" : "competitor";
                    var chunks = _chunkingService.ChunkPage(page, kw, source, competitor.Position);

                    // Set chunk ID and run ID
                    foreach (var ch in chunks)
                    {
                        ch.Id = $"{runId:N}|{taskId:N}";
                    }

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

                // 5. Create RAG Input DTO (with run_id and task_id)
                var topCompetitorDtos = serp.OrganicResults.OrderBy(c => c.Position).Take(10).Select(c => new RagCompetitorItemDto
                {
                    Position = c.Position,
                    Domain = c.Domain,
                    Url = c.Url,
                    Title = c.Title,
                    Snippet = c.Snippet,
                    WhyItRanks = c.Position <= 3
                        ? "Dominant topical authority with top keyword placement and brand trust."
                        : "Solid content depth and relevant keyword targeting matching query intent.",
                    IsTargetSite = c.IsOwnDomain || (rank.HasValue && c.Position == rank.Value)
                }).ToList();

                var ragInput = new RagInputDto
                {
                    RunId = runId.ToString(),
                    TaskId = taskId.ToString(),
                    Keyword = kw,
                    Location = loc,
                    TargetWebsite = request.Website,
                    OwnRankPosition = rank,
                    PeopleAlsoAsk = serp.PeopleAlsoAsk,
                    TopCompetitors = topCompetitorDtos,
                    IngestedChunks = ingestedChunks,
                    SystemPrompt = "Principal Technical SEO & AI Search Specialist",
                    UserPrompt = string.Empty
                };
                ragInput.UserPrompt = RagAnalysisService.BuildPrompt(ragInput);

                allRagInputs.Add(ragInput);

                // Save RAG payload snapshot to database linked to run_id
                await _auditRepository.SaveRagPayloadAsync(new RagInputPayloadEntity
                {
                    PayloadId = DbConstants.NewUuidV7(),
                    RunId = runId,
                    TaskId = taskId,
                    TargetKeyword = kw,
                    TargetLocation = loc,
                    TargetWebsite = request.Website,
                    OwnRankPosition = rank,
                    TopCompetitorsJson = JsonSerializer.Serialize(ragInput.TopCompetitors),
                    PeopleAlsoAskJson = JsonSerializer.Serialize(ragInput.PeopleAlsoAsk),
                    IngestedChunksJson = JsonSerializer.Serialize(ragInput.IngestedChunks),
                    SystemPrompt = ragInput.SystemPrompt,
                    UserPrompt = ragInput.UserPrompt,
                    CreatedAt = DateTime.UtcNow
                }, ct);
            }
        }

        // Generate baseline visibility score
        int totalPairs = report.KeywordResults.Count;
        int inTop10 = report.KeywordResults.Count(r => r.Rank.HasValue && r.Rank.Value <= 10);
        int visibility = totalPairs > 0 ? (int)((inTop10 / (double)totalPairs) * 100) : 50;
        var ranksFound = report.KeywordResults.Where(r => r.Rank.HasValue).Select(r => (decimal)r.Rank!.Value).ToList();
        decimal? avgRank = ranksFound.Count > 0 ? ranksFound.Average() : null;

        report.VisibilityScore = visibility;
        report.Summary = $"Live SERP query completed for {request.Website}. Retrieved {totalPairs} keyword checks. Found {inTop10} in top 10 positions.";
        report.FinishedAtUtc = DateTime.UtcNow;
        report.Status = "Completed";

        // Persist execution run mapping to task_runs table
        await _auditRepository.SaveTaskRunAsync(new TaskRunEntity
        {
            RunId = runId,
            TaskId = taskId,
            RunNumber = 1,
            RunStatus = "Completed",
            RunNote = request.Note,
            ImplementedChangesJson = JsonSerializer.Serialize(request.Implemented),
            VisibilityScore = visibility,
            AverageRank = avgRank,
            TotalKeywordsChecked = totalPairs,
            Top10Hits = inTop10,
            Summary = report.Summary,
            DurationSeconds = (decimal)(DateTime.UtcNow - startTime).TotalSeconds,
            StartedAt = startTime,
            FinishedAt = DateTime.UtcNow
        }, ct);

        // Save SERP Competitors linked to run_id
        await _auditRepository.SaveCompetitorsAsync(runId, taskId, competitorsToSave, ct);

        return Ok(new
        {
            success = true,
            runId = runId.ToString(),
            taskId = taskId.ToString(),
            report,
            ragInputs = allRagInputs
        });
    }

    [HttpPost]
    public async Task<IActionResult> StartAnalyzing([FromBody] RagInputDto ragInput, CancellationToken ct)
    {
        if (ragInput == null || string.IsNullOrWhiteSpace(ragInput.Keyword))
        {
            return BadRequest(new { message = "Valid RAG Input payload is required." });
        }

        _logger.LogInformation("Starting Principal SEO RAG analysis for keyword: {Keyword}, Target Site: {Site}", ragInput.Keyword, ragInput.TargetWebsite);

        var analysisResult = await _ragAnalysisService.RunCompetitiveAnalysisAsync(ragInput, ct);

        // Persist structured Gap Analysis result into PostgreSQL linked to run_id
        try
        {
            Guid runId = Guid.Empty;
            if (!string.IsNullOrWhiteSpace(ragInput.RunId) && Guid.TryParse(ragInput.RunId, out var parsedRunId))
            {
                runId = parsedRunId;
            }
            else
            {
                runId = DbConstants.NewUuidV7();
            }

            Guid taskId = Guid.Empty;
            if (!string.IsNullOrWhiteSpace(ragInput.TaskId) && Guid.TryParse(ragInput.TaskId, out var parsedTaskId))
            {
                taskId = parsedTaskId;
            }
            else
            {
                var allTasks = await _auditRepository.GetAllTasksAsync(ct);
                var matched = allTasks.FirstOrDefault(t =>
                    t.TargetWebsite.Contains(ragInput.TargetWebsite, StringComparison.OrdinalIgnoreCase));
                taskId = matched?.TaskId ?? DbConstants.NewUuidV7();
            }

            var gapEntity = new GapAnalysisResultEntity
            {
                ResultId = DbConstants.NewUuidV7(),
                RunId = runId,
                TaskId = taskId,
                LlmModel = "gemini-3.5-flash-lite",
                TargetIntentFit = analysisResult.TargetAnalysis?.CurrentIntentFit,
                OverallDrawbackSummary = analysisResult.TargetAnalysis?.OverallDrawbackSummary,
                TargetAnalysis = JsonSerializer.Serialize(analysisResult.TargetAnalysis),
                CompetitorDeepDive = JsonSerializer.Serialize(analysisResult.CompetitorDeepDive),
                GapAnalysis = JsonSerializer.Serialize(analysisResult.GapAnalysis),
                RecommendedModifications = JsonSerializer.Serialize(analysisResult.RecommendedModifications),
                RankPrediction = JsonSerializer.Serialize(analysisResult.RankPrediction),
                RawPrompt = ragInput.UserPrompt,
                RawResponse = JsonSerializer.Serialize(analysisResult),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsActive = true
            };

            await _auditRepository.SaveGapAnalysisResultAsync(gapEntity, ct);
            _logger.LogInformation("Successfully saved gap analysis result to PostgreSQL for RunId: {RunId}, TaskId: {TaskId}", runId, taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist gap analysis to PostgreSQL for run {RunId}", ragInput.RunId);
            throw new InvalidOperationException($"Unable to save RAG analysis output into database: {ex.Message}", ex);
        }

        return Ok(new
        {
            success = true,
            analysis = analysisResult
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetRunDetail(Guid id, CancellationToken ct)
    {
        var competitors = await _auditRepository.GetCompetitorsByRunIdAsync(id, ct);
        var analysis = await _auditRepository.GetGapAnalysisByRunIdAsync(id, ct);
        var ragPayload = await _auditRepository.GetRagPayloadByRunIdAsync(id, ct);

        return Ok(new
        {
            success = true,
            runId = id,
            competitors,
            analysis,
            ragPayload
        });
    }

    [HttpPost]
    public async Task<IActionResult> SaveTask([FromBody] JsonElement task, CancellationToken ct)
    {
        try
        {
            string idStr = task.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            Guid taskId = Guid.TryParse(idStr, out var g) ? g : DbConstants.NewUuidV7();
            string name = task.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";
            string website = task.TryGetProperty("website", out var wProp) ? wProp.GetString() ?? "" : "";
            string prompt = task.TryGetProperty("prompt", out var pProp) ? pProp.GetString() ?? "" : "";

            var kwList = new List<string>();
            if (task.TryGetProperty("keywords", out var kwProp) && kwProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in kwProp.EnumerateArray()) kwList.Add(el.GetString() ?? "");
            }

            var locList = new List<string>();
            if (task.TryGetProperty("locations", out var locProp) && locProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in locProp.EnumerateArray()) locList.Add(el.GetString() ?? "");
            }

            var entity = new AuditTaskEntity
            {
                TaskId = taskId,
                TaskName = name,
                TargetWebsite = website,
                TargetKeywordsJson = JsonSerializer.Serialize(kwList),
                TargetLocationsJson = JsonSerializer.Serialize(locList),
                PromptTemplate = prompt,
                CheckEngine = "live-serp-rag",
                RepeatsPerRun = 1,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                UpdatedBy = "System",
                IsActive = true
            };

            await _auditRepository.SaveTaskAsync(entity, ct);
            return Ok(new { success = true, taskId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save/edit task in PostgreSQL");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> SaveRun([FromBody] JsonElement runPayload, CancellationToken ct)
    {
        return Ok(new { success = true });
    }
}
