using System.Collections.Concurrent;
using SeoInsight.Web.Models.Domain;
using SeoInsight.Web.Services.Interfaces;

namespace SeoInsight.Web.Infrastructure.VectorStore;

/// <summary>
/// Thread-safe in-memory vector store for Phase 1 local development and testing.
/// </summary>
public sealed class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, ContentChunk> _chunks = new();
    private readonly IEmbeddingService _embeddingService;

    public InMemoryVectorStore(IEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }

    public Task DeleteByUrlAsync(string url, CancellationToken ct = default)
    {
        var keysToRemove = _chunks.Where(pair => pair.Value.Url.Equals(url, StringComparison.OrdinalIgnoreCase))
                                  .Select(pair => pair.Key)
                                  .ToList();

        foreach (var key in keysToRemove)
        {
            _chunks.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public async Task UpsertChunksAsync(IEnumerable<ContentChunk> chunks, CancellationToken ct = default)
    {
        foreach (var chunk in chunks)
        {
            // Replace old chunks for the same URL before re-ingestion
            await DeleteByUrlAsync(chunk.Url, ct);

            if (chunk.Embedding == null || chunk.Embedding.Length == 0)
            {
                chunk.Embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Content, ct);
            }

            _chunks[chunk.Id] = chunk;
        }
    }

    public async Task<List<ContentChunk>> SearchAsync(string keyword, string? sourceFilter = null, int topK = 5, CancellationToken ct = default)
    {
        var queryVector = await _embeddingService.GenerateEmbeddingAsync(keyword, ct);

        var query = _chunks.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(sourceFilter))
        {
            query = query.Where(c => string.Equals(c.Source, sourceFilter, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .Select(chunk =>
            {
                double score = chunk.Embedding != null ? CosineSimilarity(queryVector, chunk.Embedding) : 0.0;
                chunk.Score = score;
                return chunk;
            })
            .OrderByDescending(c => c.Score)
            .Take(topK)
            .ToList();
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0.0;
        double dot = 0.0, normA = 0.0, normB = 0.0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return (normA > 0 && normB > 0) ? dot / (Math.Sqrt(normA) * Math.Sqrt(normB)) : 0.0;
    }
}
