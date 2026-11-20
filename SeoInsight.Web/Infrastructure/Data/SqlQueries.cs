namespace SeoInsight.Web.Infrastructure.Data;

/// <summary>
/// Static class housing all optimized, projected SQL queries for Dapper.
/// Explicit column projections with aliases. Every child record is linked to run_id.
/// </summary>
public static class SqlQueries
{
    // --- Audit Tasks ---
    public const string GetAllTasksProjected = @"
        SELECT 
            t.task_id AS TaskId,
            t.task_name AS TaskName,
            t.target_website AS TargetWebsite,
            t.target_keywords::text AS TargetKeywordsJson,
            t.target_locations::text AS TargetLocationsJson,
            t.prompt_template AS PromptTemplate,
            t.check_engine AS CheckEngine,
            t.repeats_per_run AS RepeatsPerRun,
            t.status AS Status,
            t.created_at AS CreatedAt,
            t.updated_at AS UpdatedAt,
            t.created_by AS CreatedBy,
            t.updated_by AS UpdatedBy,
            COUNT(r.run_id)::int AS TotalRuns,
            (SELECT r2.average_rank FROM task_runs r2 WHERE r2.task_id = t.task_id ORDER BY r2.started_at DESC LIMIT 1) AS LatestAverageRank
        FROM audit_tasks t
        LEFT JOIN task_runs r ON r.task_id = t.task_id AND r.is_active = TRUE
        WHERE t.is_active = TRUE
        GROUP BY t.task_id, t.task_name, t.target_website, t.target_keywords, t.target_locations, t.prompt_template, t.check_engine, t.repeats_per_run, t.status, t.created_at, t.updated_at, t.created_by, t.updated_by
        ORDER BY t.created_at DESC;";

    public const string GetTaskByIdProjected = @"
        SELECT 
            t.task_id AS TaskId,
            t.task_name AS TaskName,
            t.target_website AS TargetWebsite,
            t.target_keywords::text AS TargetKeywordsJson,
            t.target_locations::text AS TargetLocationsJson,
            t.prompt_template AS PromptTemplate,
            t.check_engine AS CheckEngine,
            t.repeats_per_run AS RepeatsPerRun,
            t.status AS Status,
            t.created_at AS CreatedAt,
            t.updated_at AS UpdatedAt,
            t.created_by AS CreatedBy,
            t.updated_by AS UpdatedBy,
            COUNT(r.run_id)::int AS TotalRuns,
            (SELECT r2.average_rank FROM task_runs r2 WHERE r2.task_id = t.task_id ORDER BY r2.started_at DESC LIMIT 1) AS LatestAverageRank
        FROM audit_tasks t
        LEFT JOIN task_runs r ON r.task_id = t.task_id AND r.is_active = TRUE
        WHERE t.task_id = @TaskId AND t.is_active = TRUE
        GROUP BY t.task_id, t.task_name, t.target_website, t.target_keywords, t.target_locations, t.prompt_template, t.check_engine, t.repeats_per_run, t.status, t.created_at, t.updated_at, t.created_by, t.updated_by;";

    public const string UpsertTask = @"
        INSERT INTO audit_tasks (
            task_id, task_name, target_website, target_keywords, target_locations,
            prompt_template, check_engine, repeats_per_run, status, created_at, updated_at, created_by, updated_by, is_active
        ) VALUES (
            @TaskId, @TaskName, @TargetWebsite, @TargetKeywordsJson::jsonb, @TargetLocationsJson::jsonb,
            @PromptTemplate, @CheckEngine, @RepeatsPerRun, @Status, @CreatedAt, @UpdatedAt, @CreatedBy, @UpdatedBy, @IsActive
        )
        ON CONFLICT (task_id) DO UPDATE SET
            task_name = EXCLUDED.task_name,
            target_website = EXCLUDED.target_website,
            target_keywords = EXCLUDED.target_keywords,
            target_locations = EXCLUDED.target_locations,
            prompt_template = EXCLUDED.prompt_template,
            check_engine = EXCLUDED.check_engine,
            repeats_per_run = EXCLUDED.repeats_per_run,
            status = EXCLUDED.status,
            updated_at = EXCLUDED.updated_at,
            updated_by = EXCLUDED.updated_by,
            is_active = EXCLUDED.is_active;";

    public const string DeleteTaskSoft = @"
        UPDATE audit_tasks 
        SET is_active = FALSE, updated_at = clock_timestamp(), updated_by = 'System' 
        WHERE task_id = @TaskId;";

    // --- Task Runs ---
    public const string InsertTaskRun = @"
        INSERT INTO task_runs (
            run_id, task_id, run_number, run_status, run_note, implemented_changes,
            visibility_score, average_rank, total_keywords_checked, top10_hits, summary, duration_seconds,
            started_at, finished_at, created_at, updated_at, created_by, updated_by, is_active
        ) VALUES (
            @RunId, @TaskId, @RunNumber, @RunStatus, @RunNote, @ImplementedChangesJson::jsonb,
            @VisibilityScore, @AverageRank, @TotalKeywordsChecked, @Top10Hits, @Summary, @DurationSeconds,
            @StartedAt, @FinishedAt, @CreatedAt, @UpdatedAt, @CreatedBy, @UpdatedBy, @IsActive
        )
        ON CONFLICT (run_id) DO UPDATE SET
            run_status = EXCLUDED.run_status,
            run_note = EXCLUDED.run_note,
            implemented_changes = EXCLUDED.implemented_changes,
            visibility_score = EXCLUDED.visibility_score,
            average_rank = EXCLUDED.average_rank,
            total_keywords_checked = EXCLUDED.total_keywords_checked,
            top10_hits = EXCLUDED.top10_hits,
            summary = EXCLUDED.summary,
            duration_seconds = EXCLUDED.duration_seconds,
            finished_at = EXCLUDED.finished_at,
            updated_at = EXCLUDED.updated_at,
            updated_by = EXCLUDED.updated_by;";

    public const string GetRunsByTaskIdProjected = @"
        SELECT 
            r.run_id AS RunId,
            r.task_id AS TaskId,
            r.run_number AS RunNumber,
            r.run_status AS RunStatus,
            r.run_note AS RunNote,
            r.implemented_changes::text AS ImplementedChangesJson,
            r.visibility_score AS VisibilityScore,
            r.average_rank AS AverageRank,
            r.total_keywords_checked AS TotalKeywordsChecked,
            r.top10_hits AS Top10Hits,
            r.summary AS Summary,
            r.duration_seconds AS DurationSeconds,
            r.started_at AS StartedAt,
            r.finished_at AS FinishedAt,
            r.created_at AS CreatedAt,
            CASE WHEN g.result_id IS NOT NULL THEN TRUE ELSE FALSE END AS HasGapAnalysis
        FROM task_runs r
        LEFT JOIN gap_analysis_results g ON g.run_id = r.run_id
        WHERE r.task_id = @TaskId AND r.is_active = TRUE
        ORDER BY r.started_at DESC;";

    // --- SERP Competitors (Linked by run_id) ---
    public const string InsertCompetitors = @"
        INSERT INTO serp_competitors (
            competitor_id, run_id, task_id, keyword, location, position, domain, title, url, snippet, serp_highlights, is_target_site, crawled_summary, created_at, updated_at, created_by, is_active
        ) VALUES (
            @CompetitorId, @RunId, @TaskId, @Keyword, @Location, @Position, @Domain, @Title, @Url, @Snippet, @SerpHighlights, @IsTargetSite, @CrawledSummary, @CreatedAt, @UpdatedAt, @CreatedBy, @IsActive
        )
        ON CONFLICT (run_id, keyword, position) DO UPDATE SET
            domain = EXCLUDED.domain,
            title = EXCLUDED.title,
            url = EXCLUDED.url,
            snippet = EXCLUDED.snippet,
            serp_highlights = EXCLUDED.serp_highlights,
            is_target_site = EXCLUDED.is_target_site,
            crawled_summary = EXCLUDED.crawled_summary,
            updated_at = EXCLUDED.updated_at;";

    public const string GetCompetitorsByRunIdProjected = @"
        SELECT 
            c.competitor_id AS CompetitorId,
            c.run_id AS RunId,
            c.task_id AS TaskId,
            c.keyword AS Keyword,
            c.location AS Location,
            c.position AS Position,
            c.domain AS Domain,
            c.title AS Title,
            c.url AS Url,
            c.snippet AS Snippet,
            c.serp_highlights AS SerpHighlights,
            c.is_target_site AS IsTargetSite,
            c.created_at AS CreatedAt
        FROM serp_competitors c
        WHERE c.run_id = @RunId AND c.is_active = TRUE
        ORDER BY c.position ASC;";

    // --- RAG Input Payload Snapshot (Linked by run_id) ---
    public const string InsertRagPayload = @"
        INSERT INTO rag_input_payloads (
            payload_id, run_id, task_id, target_keyword, target_location, target_website, own_rank_position,
            top_competitors_json, people_also_ask, ingested_chunks_json, system_prompt, user_prompt,
            created_at, updated_at, created_by, is_active
        ) VALUES (
            @PayloadId, @RunId, @TaskId, @TargetKeyword, @TargetLocation, @TargetWebsite, @OwnRankPosition,
            @TopCompetitorsJson::jsonb, @PeopleAlsoAskJson::jsonb, @IngestedChunksJson::jsonb, @SystemPrompt, @UserPrompt,
            @CreatedAt, @UpdatedAt, @CreatedBy, @IsActive
        );";

    public const string GetRagPayloadByRunId = @"
        SELECT 
            p.payload_id AS PayloadId,
            p.run_id AS RunId,
            p.task_id AS TaskId,
            p.target_keyword AS TargetKeyword,
            p.target_location AS TargetLocation,
            p.target_website AS TargetWebsite,
            p.own_rank_position AS OwnRankPosition,
            p.top_competitors_json::text AS TopCompetitorsJson,
            p.people_also_ask::text AS PeopleAlsoAskJson,
            p.ingested_chunks_json::text AS IngestedChunksJson,
            p.system_prompt AS SystemPrompt,
            p.user_prompt AS UserPrompt,
            p.created_at AS CreatedAt
        FROM rag_input_payloads p
        WHERE p.run_id = @RunId AND p.is_active = TRUE
        LIMIT 1;";

    // --- Content Chunks (Linked by run_id) ---
    public const string InsertChunk = @"
        INSERT INTO content_chunks (
            chunk_id, run_id, task_id, source_type, source_url, competitor_rank, heading_hierarchy, chunk_text, token_count, embedding, created_at, updated_at, created_by, is_active
        ) VALUES (
            @ChunkId, @RunId, @TaskId, @SourceType, @SourceUrl, @CompetitorRank, @HeadingHierarchy, @ChunkText, @TokenCount, @Embedding, @CreatedAt, @UpdatedAt, @CreatedBy, @IsActive
        );";

    public const string GetChunksByRunIdProjected = @"
        SELECT 
            k.chunk_id AS ChunkId,
            k.run_id AS RunId,
            k.task_id AS TaskId,
            k.source_type AS SourceType,
            k.source_url AS SourceUrl,
            k.competitor_rank AS CompetitorRank,
            k.heading_hierarchy AS HeadingHierarchy,
            k.chunk_text AS ChunkText,
            k.token_count AS TokenCount,
            k.embedding AS Embedding
        FROM content_chunks k
        WHERE k.run_id = @RunId AND k.is_active = TRUE;";

    public const string GetAllChunksProjected = @"
        SELECT 
            k.chunk_id AS ChunkId,
            k.run_id AS RunId,
            k.task_id AS TaskId,
            k.source_type AS SourceType,
            k.source_url AS SourceUrl,
            k.competitor_rank AS CompetitorRank,
            k.heading_hierarchy AS HeadingHierarchy,
            k.chunk_text AS ChunkText,
            k.token_count AS TokenCount,
            k.embedding AS Embedding
        FROM content_chunks k
        WHERE k.is_active = TRUE;";

    // --- Gap Analysis Results (Linked by run_id) ---
    public const string UpsertGapAnalysisResult = @"
        INSERT INTO gap_analysis_results (
            result_id, run_id, task_id, llm_model, target_intent_fit, overall_drawback_summary,
            target_analysis, competitor_deep_dive, gap_analysis, recommended_modifications, rank_prediction,
            raw_prompt, raw_response, created_at, updated_at, created_by, is_active
        ) VALUES (
            @ResultId, @RunId, @TaskId, @LlmModel, @TargetIntentFit, @OverallDrawbackSummary,
            @TargetAnalysis::jsonb, @CompetitorDeepDive::jsonb, @GapAnalysis::jsonb, @RecommendedModifications::jsonb, @RankPrediction::jsonb,
            @RawPrompt, @RawResponse, @CreatedAt, @UpdatedAt, @CreatedBy, @IsActive
        )
        ON CONFLICT (run_id) DO UPDATE SET
            llm_model = EXCLUDED.llm_model,
            target_intent_fit = EXCLUDED.target_intent_fit,
            overall_drawback_summary = EXCLUDED.overall_drawback_summary,
            target_analysis = EXCLUDED.target_analysis,
            competitor_deep_dive = EXCLUDED.competitor_deep_dive,
            gap_analysis = EXCLUDED.gap_analysis,
            recommended_modifications = EXCLUDED.recommended_modifications,
            rank_prediction = EXCLUDED.rank_prediction,
            raw_prompt = EXCLUDED.raw_prompt,
            raw_response = EXCLUDED.raw_response,
            updated_at = EXCLUDED.updated_at;";

    public const string GetGapAnalysisByRunIdProjected = @"
        SELECT 
            g.result_id AS ResultId,
            g.run_id AS RunId,
            g.task_id AS TaskId,
            g.llm_model AS LlmModel,
            g.target_intent_fit AS TargetIntentFit,
            g.overall_drawback_summary AS OverallDrawbackSummary,
            g.target_analysis::text AS TargetAnalysisJson,
            g.competitor_deep_dive::text AS CompetitorDeepDiveJson,
            g.gap_analysis::text AS GapAnalysisJson,
            g.recommended_modifications::text AS RecommendedModificationsJson,
            g.rank_prediction::text AS RankPredictionJson,
            g.raw_prompt AS RawPrompt,
            g.raw_response AS RawResponse,
            g.created_at AS CreatedAt
        FROM gap_analysis_results g
        WHERE g.run_id = @RunId AND g.is_active = TRUE;";
}
