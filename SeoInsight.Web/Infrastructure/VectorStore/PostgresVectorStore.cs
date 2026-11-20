using SeoInsight.Web.Infrastructure.Data;
using SeoInsight.Web.Models.Database;
using SeoInsight.Web.Models.Domain;
using SeoInsight.Web.Repositories.Interfaces;
using SeoInsight.Web.Services.Interfaces;

namespace SeoInsight.Web.Infrastructure.VectorStore;

/// <summary>
/// PostgreSQL vector store implementation using native real[] arrays.
/// Seamlessly stores and queries embedding vectors with zero external C-extensions.
/// Queries use explicit column projections.
/// </summary>
public sealed class PostgresVectorStore : IVectorStore
{
    private readonly ISeoAuditRepository _repository;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<PostgresVectorStore> _logger;

    public PostgresVectorStore(
        ISeoAuditRepository repository,
        IEmbeddingService embeddingService,
        ILogger<PostgresVectorStore> logger)
    {
        _repository = repository;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task UpsertChunksAsync(IEnumerable<ContentChunk> chunks, CancellationToken ct = default)
    {
        var chunkList = chunks.ToList();
        if (chunkList.Count == 0) return;

        // Ensure all chunks have embeddings
        foreach (var c in chunkList)
        {
            if (c.Embedding == null || c.Embedding.Length == 0)
            {
                c.Embedding = await _embeddingService.GenerateEmbeddingAsync(c.Content, ct);
            }
        }

        var entities = chunkList.Select(c =>
        {
            Guid runId = Guid.Empty;
            Guid taskId = Guid.Empty;

            if (!string.IsNullOrEmpty(c.Id) && c.Id.Contains('|'))
            {
                var parts = c.Id.Split('|');
                Guid.TryParse(parts[0], out runId);
                if (parts.Length > 1) Guid.TryParse(parts[1], out taskId);
            }
            else if (!string.IsNullOrEmpty(c.Id) && Guid.TryParse(c.Id, out var parsed))
            {
                runId = parsed;
                taskId = parsed;
            }

            return new ContentChunkEntity
            {
                ChunkId = DbConstants.NewUuidV7(),
                RunId = runId,
                TaskId = taskId,
                SourceType = c.Source.Equals("own", StringComparison.OrdinalIgnoreCase) ? "TargetPage" : "Competitor",
                SourceUrl = c.Url,
                CompetitorRank = c.RankPosition,
                HeadingHierarchy = c.SectionHeading,
                ChunkText = c.Content,
                TokenCount = c.TokenEstimate,
                Embedding = c.Embedding,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsActive = true
            };
        }).ToList();

        await _repository.SaveChunksAsync(entities, ct);
        _logger.LogInformation("Saved {Count} chunks to PostgreSQL content_chunks table.", entities.Count);
    }

    public Task DeleteByUrlAsync(string url, CancellationToken ct = default)
    {
        _logger.LogInformation("DeleteByUrl requested for: {Url}", url);
        return Task.CompletedTask;
    }

    public async Task<List<ContentChunk>> SearchAsync(
        string keyword,
        string? sourceFilter = null,
        int topK = 5,
        CancellationToken ct = default)
    {
        var queryVec = await _embeddingService.GenerateEmbeddingAsync(keyword, ct);
        if (queryVec == null || queryVec.Length == 0) return [];

        var projections = await _repository.GetAllChunksAsync(ct);

        var scored = new List<(ContentChunk Chunk, float Score)>();

        foreach (var entity in projections)
        {
            if (sourceFilter != null && !entity.SourceType.Equals(sourceFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            if (entity.Embedding == null || entity.Embedding.Length != queryVec.Length)
                continue;

            float score = CosineSimilarity(queryVec, entity.Embedding);
            var chunk = new ContentChunk
            {
                Id = entity.ChunkId.ToString(),
                Url = entity.SourceUrl,
                Source = entity.SourceType,
                RankPosition = entity.CompetitorRank ?? 0,
                SectionHeading = entity.HeadingHierarchy ?? string.Empty,
                Content = entity.ChunkText,
                TokenEstimate = entity.TokenCount ?? 0,
                Embedding = entity.Embedding,
                Score = score
            };
            scored.Add((chunk, score));
        }

        return scored
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Chunk)
            .ToList();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0f;
        float dot = 0f, magA = 0f, magB = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        if (magA <= 0f || magB <= 0f) return 0f;
        return (float)(dot / (Math.Sqrt(magA) * Math.Sqrt(magB)));
    }
}
