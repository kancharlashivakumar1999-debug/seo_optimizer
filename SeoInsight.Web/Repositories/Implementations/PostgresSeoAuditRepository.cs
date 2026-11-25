using System.Data;
using System.Text.Json;
using Dapper;
using SeoInsight.Web.Infrastructure.Data;
using SeoInsight.Web.Models.Database;
using SeoInsight.Web.Models.Domain;
using SeoInsight.Web.Repositories.Interfaces;

namespace SeoInsight.Web.Repositories.Implementations;

/// <summary>
/// High-performance Dapper repository implementation persisting all SEO data to PostgreSQL.
/// All execution data (SERP competitors, chunks, RAG payloads, Gap analysis) is linked to run_id.
/// Supports Task editing and full audit columns.
/// </summary>
public sealed class PostgresSeoAuditRepository : ISeoAuditRepository, ITaskRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<PostgresSeoAuditRepository> _logger;

    public PostgresSeoAuditRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<PostgresSeoAuditRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    // --- Task Operations ---
    public async Task<IEnumerable<TaskSummaryProjection>> GetAllTasksAsync(CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QueryAsync<TaskSummaryProjection>(
            new CommandDefinition(SqlQueries.GetAllTasksProjected, cancellationToken: ct));
    }

    public async Task<TaskSummaryProjection?> GetTaskByIdAsync(Guid taskId, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<TaskSummaryProjection>(
            new CommandDefinition(SqlQueries.GetTaskByIdProjected, new { TaskId = taskId }, cancellationToken: ct));
    }

    public async Task<Guid> SaveTaskAsync(AuditTaskEntity task, CancellationToken ct = default)
    {
        if (task.TaskId == Guid.Empty)
        {
            task.TaskId = DbConstants.NewUuidV7();
        }

        task.UpdatedAt = DateTime.UtcNow;

        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(SqlQueries.UpsertTask, task, cancellationToken: ct));
        return task.TaskId;
    }

    public async Task<bool> DeleteTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(SqlQueries.DeleteTaskSoft, new { TaskId = taskId }, cancellationToken: ct));
        return rows > 0;
    }

    // --- Task Runs Mapping ---
    public async Task<Guid> SaveTaskRunAsync(TaskRunEntity run, CancellationToken ct = default)
    {
        if (run.RunId == Guid.Empty)
        {
            run.RunId = DbConstants.NewUuidV7();
        }

        if (string.IsNullOrWhiteSpace(run.ImplementedChangesJson))
        {
            run.ImplementedChangesJson = "[]";
        }

        run.UpdatedAt = DateTime.UtcNow;

        try
        {
            using var conn = _connectionFactory.CreateConnection();
            await conn.ExecuteAsync(new CommandDefinition(SqlQueries.InsertTaskRun, run, cancellationToken: ct));
            return run.RunId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save task run record into task_runs table. RunId: {RunId}, TaskId: {TaskId}, Error: {Message}", run.RunId, run.TaskId, ex.Message);
            throw new InvalidOperationException($"Unable to persist task run {run.RunId} for task {run.TaskId}: {ex.Message}", ex);
        }
    }

    public async Task<IEnumerable<TaskRunSummaryProjection>> GetRunsByTaskIdAsync(Guid taskId, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QueryAsync<TaskRunSummaryProjection>(
            new CommandDefinition(SqlQueries.GetRunsByTaskIdProjected, new { TaskId = taskId }, cancellationToken: ct));
    }

    // --- SERP Competitors (Linked by run_id) ---
    public async Task SaveCompetitorsAsync(Guid runId, Guid taskId, IEnumerable<SerpCompetitorEntity> competitors, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var transaction = conn.BeginTransaction();

        try
        {
            foreach (var comp in competitors)
            {
                if (comp.CompetitorId == Guid.Empty) comp.CompetitorId = DbConstants.NewUuidV7();
                comp.RunId = runId;
                comp.TaskId = taskId;
                await conn.ExecuteAsync(new CommandDefinition(SqlQueries.InsertCompetitors, comp, transaction: transaction, cancellationToken: ct));
            }
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Failed to save SERP competitors for run {RunId}", runId);
            throw;
        }
    }

    public async Task<IEnumerable<CompetitorItemProjection>> GetCompetitorsByRunIdAsync(Guid runId, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QueryAsync<CompetitorItemProjection>(
            new CommandDefinition(SqlQueries.GetCompetitorsByRunIdProjected, new { RunId = runId }, cancellationToken: ct));
    }

    // --- RAG Input Payload Snapshot (Linked by run_id) ---
    public async Task SaveRagPayloadAsync(RagInputPayloadEntity payload, CancellationToken ct = default)
    {
        if (payload.PayloadId == Guid.Empty) payload.PayloadId = DbConstants.NewUuidV7();

        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(SqlQueries.InsertRagPayload, payload, cancellationToken: ct));
    }

    public async Task<RagInputPayloadEntity?> GetRagPayloadByRunIdAsync(Guid runId, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<RagInputPayloadEntity>(
            new CommandDefinition(SqlQueries.GetRagPayloadByRunId, new { RunId = runId }, cancellationToken: ct));
    }

    // --- Content Chunks (Linked by run_id) ---
    public async Task SaveChunksAsync(IEnumerable<ContentChunkEntity> chunks, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var transaction = conn.BeginTransaction();

        try
        {
            foreach (var chunk in chunks)
            {
                if (chunk.ChunkId == Guid.Empty) chunk.ChunkId = DbConstants.NewUuidV7();
                await conn.ExecuteAsync(new CommandDefinition(SqlQueries.InsertChunk, chunk, transaction: transaction, cancellationToken: ct));
            }
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Failed to save content chunks");
            throw;
        }
    }

    public async Task<IEnumerable<ContentChunkProjection>> GetChunksByRunIdAsync(Guid runId, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QueryAsync<ContentChunkProjection>(
            new CommandDefinition(SqlQueries.GetChunksByRunIdProjected, new { RunId = runId }, cancellationToken: ct));
    }

    public async Task<IEnumerable<ContentChunkProjection>> GetAllChunksAsync(CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QueryAsync<ContentChunkProjection>(
            new CommandDefinition(SqlQueries.GetAllChunksProjected, cancellationToken: ct));
    }

    // --- Gap Analysis Results (Linked by run_id) ---
    public async Task SaveGapAnalysisResultAsync(GapAnalysisResultEntity analysisResult, CancellationToken ct = default)
    {
        if (analysisResult.ResultId == Guid.Empty) analysisResult.ResultId = DbConstants.NewUuidV7();

        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(SqlQueries.UpsertGapAnalysisResult, analysisResult, cancellationToken: ct));
    }

    public async Task<GapAnalysisProjection?> GetGapAnalysisByRunIdAsync(Guid runId, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<GapAnalysisProjection>(
            new CommandDefinition(SqlQueries.GetGapAnalysisByRunIdProjected, new { RunId = runId }, cancellationToken: ct));
    }

    // --- Legacy RunReport Compatibility ---
    public async Task<List<RunReport>> GetAllReportsAsync(CancellationToken ct = default)
    {
        try
        {
            var tasks = await GetAllTasksAsync(ct);
            var list = new List<RunReport>();
            foreach (var t in tasks)
            {
                var runs = await GetRunsByTaskIdAsync(t.TaskId, ct);
                foreach (var r in runs)
                {
                    list.Add(new RunReport
                    {
                        Id = r.RunId.ToString(),
                        TaskName = t.TaskName,
                        Website = t.TargetWebsite,
                        StartedAtUtc = r.StartedAt,
                        FinishedAtUtc = r.FinishedAt,
                        Status = r.RunStatus,
                        VisibilityScore = r.VisibilityScore,
                        Summary = r.Summary ?? string.Empty
                    });
                }
            }
            return list.OrderByDescending(r => r.StartedAtUtc).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading reports from PostgreSQL");
            return [];
        }
    }

    // --- Debug Logs ---
    public async Task SaveSerpDebugLogAsync(SerpRequestDebugLogEntity log, CancellationToken ct = default)
    {
        if (log.Id == Guid.Empty)
        {
            log.Id = DbConstants.NewUuidV7();
        }

        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(SqlQueries.InsertSerpDebugLog, log, cancellationToken: ct));
    }

    public async Task<IEnumerable<SerpRequestDebugLogEntity>> GetSerpDebugLogsByRunIdAsync(Guid runId, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QueryAsync<SerpRequestDebugLogEntity>(
            new CommandDefinition(SqlQueries.GetSerpDebugLogsByRunId, new { RunId = runId }, cancellationToken: ct));
    }

    public async Task<RunReport?> GetReportByIdAsync(string id, CancellationToken ct = default)
    {
        var all = await GetAllReportsAsync(ct);
        return all.FirstOrDefault(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public Task SaveReportAsync(RunReport report, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
