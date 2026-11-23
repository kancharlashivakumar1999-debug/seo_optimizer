using SeoInsight.Web.Models.Domain;

namespace SeoInsight.Web.Services.Interfaces;

/// <summary>
/// Facade service for querying SERP data using the configured provider.
/// </summary>
public interface ISerpService
{
    Task<SerpResult> GetSerpAsync(string keyword, string location, CancellationToken ct = default);
    Task<SerpResult> GetSerpAsync(string keyword, string location, int page, CancellationToken ct = default);
}
