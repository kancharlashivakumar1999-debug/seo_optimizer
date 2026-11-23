using Microsoft.Extensions.Options;
using SeoInsight.Web.Models.Domain;
using SeoInsight.Web.Models.Options;
using SeoInsight.Web.Services.Interfaces;

namespace SeoInsight.Web.Services.Implementations;

public sealed class SerpService : ISerpService
{
    private readonly IEnumerable<ISerpProvider> _providers;
    private readonly IOptionsMonitor<SerpOptions> _optionsMonitor;
    private readonly ILogger<SerpService> _logger;

    public SerpService(
        IEnumerable<ISerpProvider> providers,
        IOptionsMonitor<SerpOptions> optionsMonitor,
        ILogger<SerpService> logger)
    {
        _providers = providers;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<SerpResult> GetSerpAsync(string keyword, string location, CancellationToken ct = default)
    {
        return await GetSerpAsync(keyword, location, 1, ct);
    }

    public async Task<SerpResult> GetSerpAsync(string keyword, string location, int page, CancellationToken ct = default)
    {
        var providerName = _optionsMonitor.CurrentValue.Provider;
        var provider = _providers.FirstOrDefault(p => string.Equals(p.ProviderName, providerName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No SERP provider registered with name '{providerName}'.");

        _logger.LogInformation("Querying SERP via provider '{ProviderName}' for keyword '{Keyword}', location '{Location}', page {Page}",
            providerName, keyword, location, page);

        return await provider.FetchSerpAsync(keyword, location, page, ct);
    }
}
