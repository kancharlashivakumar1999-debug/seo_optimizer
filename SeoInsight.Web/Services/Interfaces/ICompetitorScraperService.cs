using SeoInsight.Web.Models.Domain;

namespace SeoInsight.Web.Services.Interfaces;

/// <summary>
/// Scrapes and extracts structured content from web pages.
/// </summary>
public interface ICompetitorScraperService
{
    Task<CompetitorPage> ScrapeAsync(string url, CancellationToken ct = default);
}
