using SeoInsight.Web.Models.Domain;

namespace SeoInsight.Web.Services.Interfaces;

/// <summary>
/// Vector store abstraction for indexing and similarity search.
/// </summary>
public interface IVectorStore
{
    Task UpsertChunksAsync(IEnumerable<ContentChunk> chunks, CancellationToken ct = default);
    Task DeleteByUrlAsync(string url, CancellationToken ct = default);
    Task<List<ContentChunk>> SearchAsync(string keyword, string? sourceFilter = null, int topK = 5, CancellationToken ct = default);
}
