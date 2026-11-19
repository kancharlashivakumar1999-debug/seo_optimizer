using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using SeoInsight.Web.Constants;
using SeoInsight.Web.Infrastructure.VectorStore;
using SeoInsight.Web.Models.Options;
using SeoInsight.Web.Repositories.Implementations;
using SeoInsight.Web.Repositories.Interfaces;
using SeoInsight.Web.Services.Implementations;
using SeoInsight.Web.Services.Interfaces;

namespace SeoInsight.Web.Extensions;

/// <summary>
/// Service collection extension methods to keep Program.cs clean.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSeoInsightConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SerpOptions>()
            .Bind(configuration.GetSection(SerpOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ScraperOptions>()
            .Bind(configuration.GetSection(ScraperOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<GoogleGeminiOptions>()
            .Bind(configuration.GetSection(GoogleGeminiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RagOptions>()
            .Bind(configuration.GetSection(RagOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AzureAISearchOptions>()
            .Bind(configuration.GetSection(AzureAISearchOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<TrackingOptions>()
            .Bind(configuration.GetSection(TrackingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddSeoInsightHttpClients(this IServiceCollection services)
    {
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30));

        services.AddHttpClient("ScraperClient", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ScraperOptions>>().Value;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        })
        .AddPolicyHandler(retryPolicy)
        .AddPolicyHandler(timeoutPolicy);

        services.AddHttpClient("DataForSeoClient", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<SerpOptions>>().Value;
            if (!string.IsNullOrEmpty(options.BaseUrl))
            {
                client.BaseAddress = new Uri(options.BaseUrl);
            }
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        })
        .AddPolicyHandler(retryPolicy)
        .AddPolicyHandler(timeoutPolicy);

        services.AddHttpClient("SerpApiClient", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<SerpOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        })
        .AddPolicyHandler(retryPolicy)
        .AddPolicyHandler(timeoutPolicy);

        services.AddHttpClient("GoogleGeminiClient", (sp, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        })
        .AddPolicyHandler(retryPolicy)
        .AddPolicyHandler(timeoutPolicy);

        return services;
    }

    public static IServiceCollection AddSeoInsightCoreServices(this IServiceCollection services)
    {
        // SERP Providers
        services.AddScoped<ISerpProvider, DataForSeoProvider>();
        services.AddScoped<ISerpProvider, SerpApiProvider>();
        services.AddScoped<ISerpProvider, SemrushProvider>();
        services.AddScoped<ISerpService, SerpService>();

        // Scraper & Chunking
        services.AddScoped<ICompetitorScraperService, CompetitorScraperService>();
        services.AddScoped<IChunkingService, ChunkingService>();

        // Embeddings, Vector Store & RAG Analysis
        services.AddSingleton<IEmbeddingService, EmbeddingService>();
        services.AddSingleton<IVectorStore, InMemoryVectorStore>();
        services.AddScoped<IRagAnalysisService, RagAnalysisService>();

        // Repositories
        services.AddSingleton<ITaskRepository, TaskRepository>();

        return services;
    }
}
