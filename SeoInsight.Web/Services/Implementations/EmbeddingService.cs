using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;
using SeoInsight.Web.Models.Options;
using SeoInsight.Web.Services.Interfaces;

namespace SeoInsight.Web.Services.Implementations;

public sealed class EmbeddingService : IEmbeddingService
{
    private readonly IOptionsMonitor<AzureOpenAIOptions> _azureOptions;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(IOptionsMonitor<AzureOpenAIOptions> azureOptions, ILogger<EmbeddingService> logger)
    {
        _azureOptions = azureOptions;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var opt = _azureOptions.CurrentValue;

        if (string.IsNullOrWhiteSpace(opt.Endpoint) || string.IsNullOrWhiteSpace(opt.ApiKey))
        {
            // Dev deterministic in-memory vector embedding (1536 float elements)
            return GenerateDeterministicEmbedding(text, 1536);
        }

        try
        {
            var client = new AzureOpenAIClient(new Uri(opt.Endpoint), new AzureKeyCredential(opt.ApiKey));
            var embeddingClient = client.GetEmbeddingClient(opt.EmbeddingDeployment);
            var response = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: ct);
            return response.Value.ToFloats().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI embedding request failed. Generating fallback vector representation.");
            return GenerateDeterministicEmbedding(text, 1536);
        }
    }

    private static float[] GenerateDeterministicEmbedding(string text, int dimensions)
    {
        var vector = new float[dimensions];
        int hash = 17;
        foreach (char c in text)
        {
            hash = hash * 31 + c;
        }

        var rand = new Random(hash);
        float norm = 0f;
        for (int i = 0; i < dimensions; i++)
        {
            vector[i] = (float)(rand.NextDouble() * 2.0 - 1.0);
            norm += vector[i] * vector[i];
        }

        norm = MathF.Sqrt(norm);
        if (norm > 0)
        {
            for (int i = 0; i < dimensions; i++)
            {
                vector[i] /= norm;
            }
        }

        return vector;
    }
}
