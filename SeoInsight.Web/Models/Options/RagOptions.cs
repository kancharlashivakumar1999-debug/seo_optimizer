using System.ComponentModel.DataAnnotations;

namespace SeoInsight.Web.Models.Options;

/// <summary>
/// Strongly-typed options for the RAG pipeline.
/// </summary>
public sealed class RagOptions
{
    public const string SectionName = "Rag";

    [Required]
    public string VectorStoreProvider { get; set; } = "InMemory";

    [Range(50, 4000)]
    public int ChunkSizeTokens { get; set; } = 700;

    [Range(0, 50)]
    public int ChunkOverlapPercent { get; set; } = 12;

    [Range(1, 20)]
    public int TopKCompetitorChunks { get; set; } = 5;

    [Range(1, 20)]
    public int TopKOwnChunks { get; set; } = 3;

    [Range(0.0, 1.0)]
    public double MinRelevanceScore { get; set; } = 0.75;
}
