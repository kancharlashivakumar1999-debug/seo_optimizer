namespace SeoInsight.Web.Services.Interfaces;

/// <summary>
/// Embedding generator contract.
/// </summary>
public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
}
