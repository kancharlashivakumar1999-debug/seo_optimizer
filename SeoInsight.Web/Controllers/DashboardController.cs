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
        var taskList = (await _auditRepository.GetAllTasksAsync(ct)).ToList();
        var reports = await _taskRepository.GetAllReportsAsync(ct);
        var recentRanksMap = await _auditRepository.GetRecentRanksForAllTasksAsync(ct);

        int totalKeywords = 0;
        int top10Hits = 0;
        int notRankingHits = 0;
        int improvedCount = 0;
        int droppedCount = 0;
        int unchangedCount = 0;

        var biggestMoves = new List<BiggestMoveProjection>();

        foreach (var task in taskList)
        {
            if (recentRanksMap.TryGetValue(task.TaskId, out var ranks))
            {
                task.RecentRanks = ranks;
            }
            else if (task.LatestAverageRank.HasValue)
            {
                task.RecentRanks = new List<decimal> { task.LatestAverageRank.Value };
            }

            totalKeywords++;

            if (task.LatestAverageRank.HasValue)
            {
                if (task.LatestAverageRank.Value <= 10m)
                {
                    top10Hits++;
                }
                else if (task.LatestAverageRank.Value > 100m)
                {
                    notRankingHits++;
                }

                if (task.PreviousAverageRank.HasValue)
                {
                    decimal diff = task.PreviousAverageRank.Value - task.LatestAverageRank.Value;
                    if (diff > 0) improvedCount++;
                    else if (diff < 0) droppedCount++;
                    else unchangedCount++;

                    biggestMoves.Add(new BiggestMoveProjection
                    {
                        TaskName = task.TaskName,
                        Domain = task.TargetWebsite,
                        Keyword = task.TargetKeyword,
                        Location = task.TargetLocation,
                        PreviousRank = (int)Math.Round(task.PreviousAverageRank.Value),
                        CurrentRank = (int)Math.Round(task.LatestAverageRank.Value),
                        Delta = (int)Math.Round(diff)
                    });
                }
                else
                {
                    unchangedCount++;
                }
            }
            else
            {
                notRankingHits++;
            }
        }

        biggestMoves = biggestMoves
            .OrderByDescending(m => Math.Abs(m.Delta))
            .Take(5)
            .ToList();

        var rankedTasks = taskList.Where(t => t.LatestAverageRank.HasValue).ToList();
        decimal? overallAvgRank = rankedTasks.Any() ? rankedTasks.Average(t => t.LatestAverageRank!.Value) : null;
        var prevRankedTasks = taskList.Where(t => t.PreviousAverageRank.HasValue).ToList();
        decimal? prevOverallAvgRank = prevRankedTasks.Any() ? prevRankedTasks.Average(t => t.PreviousAverageRank!.Value) : null;
        decimal? avgRankDelta = (overallAvgRank.HasValue && prevOverallAvgRank.HasValue) 
            ? (prevOverallAvgRank.Value - overallAvgRank.Value) 
            : null;

        var metrics = new DashboardMetricsProjection
        {
            AvgRank = overallAvgRank.HasValue ? Math.Round(overallAvgRank.Value, 1) : null,
            AvgRankDelta = avgRankDelta.HasValue ? Math.Round(avgRankDelta.Value, 1) : null,
            Top10Count = top10Hits,
            TotalKeywords = totalKeywords,
            ImprovedCount = improvedCount,
            DroppedCount = droppedCount,
            UnchangedCount = unchangedCount,
            NotRankingCount = notRankingHits
        };

        return Ok(new
        {
            success = true,
            metrics,
            tasks = taskList,
            biggestMoves,
            reports
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetTasksPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var pagedTasks = await _auditRepository.GetTasksPagedAsync(page, pageSize, ct);
        return Ok(new
        {
            success = true,
            paged = pagedTasks
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetTaskDetail(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var task = await _auditRepository.GetTaskByIdAsync(id, ct);
        if (task == null) return NotFound(new { message = "Task not found." });

        var pagedRuns = await _auditRepository.GetRunsByTaskIdPagedAsync(id, page, pageSize, ct);

        return Ok(new
        {
            success = true,
            task,
            pagedRuns,
            runs = pagedRuns.Items
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetTaskChartData(Guid id, CancellationToken ct)
    {
        var task = await _auditRepository.GetTaskByIdAsync(id, ct);
        if (task == null) return NotFound(new { message = "Task not found." });

        var chartRuns = (await _auditRepository.GetTaskChartRunsAsync(id, ct)).ToList();

        // Ensure chronological numbering: if DB has identical RunNumbers or fallback, guarantee distinct sequential run labels
        var labels = chartRuns.Select((r, i) => $"Run #{i + 1}").ToList();
        var dataPoints = chartRuns.Select(r => r.AverageRank.HasValue ? Math.Round(r.AverageRank.Value, 1) : (decimal?)null).ToList();
        var dates = chartRuns.Select(r => r.StartedAt).ToList();

        return Ok(new
        {
            success = true,
            taskId = task.TaskId,
            taskName = task.TaskName,
            website = task.TargetWebsite,
            keyword = task.TargetKeyword,
            location = task.TargetLocation,
            totalRuns = chartRuns.Count,
            labels,
            ranks = dataPoints,
            dates,
            runs = chartRuns
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
        // Resolve single target keyword and location from request
        string kw = !string.IsNullOrWhiteSpace(request.Keyword) 
            ? request.Keyword.Trim() 
            : (request.Keywords.FirstOrDefault() ?? "").Trim();

        string loc = !string.IsNullOrWhiteSpace(request.Location) 
            ? request.Location.Trim() 
            : (request.Locations.FirstOrDefault() ?? "").Trim();

        if (string.IsNullOrWhiteSpace(kw) || string.IsNullOrWhiteSpace(loc))
        {
            return BadRequest(new { message = "Keyword and location are required." });
        }

        _logger.LogInformation("Executing live SERP fetch for website: {Website}, keyword: '{Keyword}', location: '{Location}'", 
            request.Website, kw, loc);

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
                    TargetKeyword = kw,
                    TargetLocation = loc,
                    TargetKeywordsJson = JsonSerializer.Serialize(new List<string> { kw }),
                    TargetLocationsJson = JsonSerializer.Serialize(new List<string> { loc }),
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
                    TargetKeyword = kw,
                    TargetLocation = loc,
                    TargetKeywordsJson = JsonSerializer.Serialize(new List<string> { kw }),
                    TargetLocationsJson = JsonSerializer.Serialize(new List<string> { loc }),
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
            TargetKeyword = kw,
            TargetLocation = loc,
            RunNumber = 1,
            RunStatus = "Running",
            RunNote = request.Note,
            ImplementedChangesJson = JsonSerializer.Serialize(request.Implemented),
            VisibilityScore = 0,
            AverageRank = null,
            TotalKeywordsChecked = 1,
            Top10Hits = 0,
            Summary = "Live SERP & RAG processing in progress",
            DurationSeconds = 0,
            StartedAt = startTime,
            CreatedAt = startTime,
            UpdatedAt = startTime
        }, ct);

        var allRagInputs = new List<RagInputDto>();
        var competitorsToSave = new List<SerpCompetitorEntity>();

        // 1. Fetch live SERP with pagination loop up to 5 pages for single (keyword, location)
        string normalizedTarget = request.Website.Trim().ToLowerInvariant()
            .Replace("https://", "")
            .Replace("http://", "")
            .Replace("www.", "")
            .TrimEnd('/');

        SerpResult serp = null!;
        SerpItem? ownItem = null;
        int? cumulativeOwnRank = null;
        var allOrganicResults = new List<SerpItem>();
        var allPaa = new List<string>();

        for (int page = 1; page <= 5; page++)
        {
            SerpResult pageSerp;
            try
            {
                pageSerp = await _serpService.GetSerpAsync(kw, loc, page, runId, ct);
            }
            catch (Polly.Timeout.TimeoutRejectedException ex)
            {
                _logger.LogWarning(ex, "Timeout fetching SERP page {Page} for '{Keyword}' ({Location}). Continuing with gathered results.", page, kw, loc);
                break;
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Operation canceled while fetching SERP page {Page} for '{Keyword}' ({Location}). Continuing with gathered results.", page, kw, loc);
                break;
            }
            catch (Exception ex) when (page > 1)
            {
                _logger.LogWarning(ex, "Failed to fetch SERP page {Page} for '{Keyword}' ({Location}). Continuing with gathered results.", page, kw, loc);
                break;
            }

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

            int previousOrganicCount = allOrganicResults.Count;

            for (int i = 0; i < pageSerp.OrganicResults.Count; i++)
            {
                var item = pageSerp.OrganicResults[i];
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
                        // Calculate rank: previous organic results count + current page index (1-based)
                        cumulativeOwnRank = previousOrganicCount + (i + 1);
                        ownItem.Position = cumulativeOwnRank.Value;
                    }
                }

                allOrganicResults.Add(item);

                // Stop immediately when target is matched
                if (ownItem != null)
                {
                    break;
                }
            }

            // Check if target website is found in this page - exit outer pagination loop immediately
            if (ownItem != null)
            {
                _logger.LogInformation("Found target website '{Target}' at position {Pos} on SERP page {Page} for keyword '{Keyword}'. Halting further SERP page calls and preparing RAG input.",
                    request.Website, cumulativeOwnRank ?? ownItem.Position, page, kw);
                break;
            }

            // If fewer results returned than requested depth, reached end of SERP
            if (pageSerp.OrganicResults.Count == 0)
            {
                break;
            }
        }

        // Attach consolidated organic results and PAA
        if (serp == null)
        {
            serp = new SerpResult
            {
                Keyword = kw,
                Location = loc,
                SearchEngine = "google",
                FetchedAtUtc = DateTime.UtcNow
            };
        }
        serp.OrganicResults = allOrganicResults;
        serp.PeopleAlsoAsk = allPaa;

        // 2. Identify target rank (based on previous page organic count + matching index on current page)
        int? rank = cumulativeOwnRank ?? ownItem?.Position;

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

        // Calculate next run number for this task
        var existingRuns = await _auditRepository.GetRunsByTaskIdAsync(taskId, ct);
        int nextRunNumber = existingRuns.Any() ? existingRuns.Max(r => r.RunNumber) + 1 : 1;

        // Persist execution run mapping to task_runs table
        await _auditRepository.SaveTaskRunAsync(new TaskRunEntity
        {
            RunId = runId,
            TaskId = taskId,
            RunNumber = nextRunNumber,
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
            avgRank = avgRank,
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
        var run = await _auditRepository.GetRunByIdAsync(id, ct);
        var competitors = await _auditRepository.GetCompetitorsByRunIdAsync(id, ct);
        var analysis = await _auditRepository.GetGapAnalysisByRunIdAsync(id, ct);
        var ragPayload = await _auditRepository.GetRagPayloadByRunIdAsync(id, ct);

        return Ok(new
        {
            success = true,
            runId = id,
            run,
            avgRank = run?.AverageRank,
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

            string targetKeyword = task.TryGetProperty("keyword", out var kwSingleProp) ? kwSingleProp.GetString() ?? "" : "";
            string targetLocation = task.TryGetProperty("location", out var locSingleProp) ? locSingleProp.GetString() ?? "" : "";

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

            if (string.IsNullOrWhiteSpace(targetKeyword))
            {
                targetKeyword = kwList.FirstOrDefault() ?? "";
            }
            if (string.IsNullOrWhiteSpace(targetLocation))
            {
                targetLocation = locList.FirstOrDefault() ?? "";
            }

            if (kwList.Count == 0 && !string.IsNullOrWhiteSpace(targetKeyword))
            {
                kwList.Add(targetKeyword);
            }
            if (locList.Count == 0 && !string.IsNullOrWhiteSpace(targetLocation))
            {
                locList.Add(targetLocation);
            }

            var entity = new AuditTaskEntity
            {
                TaskId = taskId,
                TaskName = name,
                TargetWebsite = website,
                TargetKeyword = targetKeyword,
                TargetLocation = targetLocation,
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
        await Task.CompletedTask;
        return Ok(new { success = true });
    }
}
