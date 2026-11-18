using System.ComponentModel.DataAnnotations;

namespace SeoInsight.Web.Models.Options;

/// <summary>
/// Strongly-typed options for Azure OpenAI.
/// </summary>
public sealed class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ChatDeployment { get; set; } = "gpt-4o";

    public string EmbeddingDeployment { get; set; } = "text-embedding-3-small";

    [Range(0.0, 2.0)]
    public double Temperature { get; set; } = 0.2;

    [Range(100, 10000)]
    public int MaxOutputTokens { get; set; } = 2000;
}
