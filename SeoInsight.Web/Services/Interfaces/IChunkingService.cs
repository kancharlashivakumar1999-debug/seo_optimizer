using SeoInsight.Web.Models.Domain;

namespace SeoInsight.Web.Services.Interfaces;

/// <summary>
/// Chunks clean competitor and own web page content into heading-scoped chunks.
/// </summary>
public interface IChunkingService
{
    List<ContentChunk> ChunkPage(CompetitorPage page, string keyword, string source, int rankPosition);
}
