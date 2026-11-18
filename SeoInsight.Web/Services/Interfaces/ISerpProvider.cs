using SeoInsight.Web.Models.Domain;

namespace SeoInsight.Web.Services.Interfaces;

/// <summary>
/// SERP provider contract for external search engine ranking integrations.
/// </summary>
public interface ISerpProvider
{
    string ProviderName { get; }

    /// <summary>
    /// Executes a SERP lookup and normalizes into a domain SerpResult.
    /// </summary>
    Task<SerpResult> FetchSerpAsync(string keyword, string location, CancellationToken ct = default);
}
