using SeoInsight.Web.Constants;
using SeoInsight.Web.Models.Domain;
using SeoInsight.Web.Services.Interfaces;

namespace SeoInsight.Web.Services.Implementations;

public sealed class SemrushProvider : ISerpProvider
{
    public string ProviderName => ProviderConstants.Semrush;

    public Task<SerpResult> FetchSerpAsync(string keyword, string location, CancellationToken ct = default)
        => FetchSerpAsync(keyword, location, 1, ct);

    public Task<SerpResult> FetchSerpAsync(string keyword, string location, int page, CancellationToken ct = default)
    {
        throw new NotSupportedException($"The SERP provider '{ProviderConstants.Semrush}' is a stub in Phase 1. Use '{ProviderConstants.SerpApi}'.");
    }
}
