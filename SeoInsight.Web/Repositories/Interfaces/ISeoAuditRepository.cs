using SeoInsight.Web.Models.Database;
using SeoInsight.Web.Models.Domain;

namespace SeoInsight.Web.Repositories.Interfaces;

/// <summary>
/// High-performance Dapper repository interface following ISP.
/// Provides run-isolated data queries with explicit projected column mappings.
/// </summary>
public interface ISeoAuditRepository
{
    // Task Operations (Including Edit & Soft Delete)
    Task<IEnumerable<TaskSummaryProjection>> GetAllTasksAsync(CancellationToken ct = default);
    Task<TaskSummaryProjection?> GetTaskByIdAsync(Guid taskId, CancellationToken ct = default);
    Task<Guid> SaveTaskAsync(AuditTaskEntity task, CancellationToken ct = default);
    Task<bool> DeleteTaskAsync(Guid taskId, CancellationToken ct = default);

    // Task Run Operations (Execution Mapping)
    Task<Guid> SaveTaskRunAsync(TaskRunEntity run, CancellationToken ct = default);
    Task<IEnumerable<TaskRunSummaryProjection>> GetRunsByTaskIdAsync(Guid taskId, CancellationToken ct = default);

    // SERP Competitor Operations (Linked by run_id)
    Task SaveCompetitorsAsync(Guid runId, Guid taskId, IEnumerable<SerpCompetitorEntity> competitors, CancellationToken ct = default);
    Task<IEnumerable<CompetitorItemProjection>> GetCompetitorsByRunIdAsync(Guid runId, CancellationToken ct = default);

    // RAG Input Payload Snapshot Operations (Linked by run_id)
    Task SaveRagPayloadAsync(RagInputPayloadEntity payload, CancellationToken ct = default);
    Task<RagInputPayloadEntity?> GetRagPayloadByRunIdAsync(Guid runId, CancellationToken ct = default);

    // Chunks & Vectors (Linked by run_id)
    Task SaveChunksAsync(IEnumerable<ContentChunkEntity> chunks, CancellationToken ct = default);
    Task<IEnumerable<ContentChunkProjection>> GetChunksByRunIdAsync(Guid runId, CancellationToken ct = default);
    Task<IEnumerable<ContentChunkProjection>> GetAllChunksAsync(CancellationToken ct = default);

    // Gap Analysis Operations (Linked by run_id)
    Task SaveGapAnalysisResultAsync(GapAnalysisResultEntity analysisResult, CancellationToken ct = default);
    Task<GapAnalysisProjection?> GetGapAnalysisByRunIdAsync(Guid runId, CancellationToken ct = default);

    // Legacy RunReport Compatibility
    Task<List<RunReport>> GetAllReportsAsync(CancellationToken ct = default);
    Task<RunReport?> GetReportByIdAsync(string id, CancellationToken ct = default);
    Task SaveReportAsync(RunReport report, CancellationToken ct = default);
}
