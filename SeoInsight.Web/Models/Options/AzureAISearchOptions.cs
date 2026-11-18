using System.ComponentModel.DataAnnotations;

namespace SeoInsight.Web.Models.Options;

/// <summary>
/// Strongly-typed options for Azure AI Search vector store.
/// </summary>
public sealed class AzureAISearchOptions
{
    public const string SectionName = "AzureAISearch";

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string IndexName { get; set; } = "seo-content";
}
