using SeoInsight.Web.Constants;
using SeoInsight.Web.Models.Domain;
using SeoInsight.Web.Services.Interfaces;

namespace SeoInsight.Web.Services.Implementations;

public sealed class SerpApiProvider : ISerpProvider
{
    public string ProviderName => ProviderConstants.SerpApi;

    public Task<SerpResult> FetchSerpAsync(string keyword, string location, CancellationToken ct = default)
    {
        throw new NotSupportedException($"The SERP provider '{ProviderConstants.SerpApi}' is a stub in Phase 1. Use '{ProviderConstants.DataForSeo}'.");
    }
}

public sealed class SemrushProvider : ISerpProvider
{
    public string ProviderName => ProviderConstants.Semrush;

    public Task<SerpResult> FetchSerpAsync(string keyword, string location, CancellationToken ct = default)
    {
        throw new NotSupportedException($"The SERP provider '{ProviderConstants.Semrush}' is a stub in Phase 1. Use '{ProviderConstants.DataForSeo}'.");
    }
}
