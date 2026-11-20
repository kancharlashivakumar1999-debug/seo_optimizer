using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SeoInsight.Web.Models.Options;
using SeoInsight.Web.Services.Interfaces;

namespace SeoInsight.Web.Services.Implementations;

/// <summary>
/// Embedding generator implementation using Google Gemini Embedding model (text-embedding-004).
/// Replaces AzureOpenAIClient with Google Gemini API.
/// </summary>
public sealed class EmbeddingService : IEmbeddingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<GoogleGeminiOptions> _geminiOptions;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<GoogleGeminiOptions> geminiOptions,
        ILogger<EmbeddingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _geminiOptions = geminiOptions;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var opt = _geminiOptions.CurrentValue;

        if (string.IsNullOrWhiteSpace(opt.ApiKey))
        {
            return GenerateDeterministicEmbedding(text, 768);
        }

        try
        {
            var client = _httpClientFactory.CreateClient("GoogleGeminiClient");
            var embeddingModel = "text-embedding-004";
            var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{embeddingModel}:embedContent?key={opt.ApiKey}";

            var requestPayload = new
            {
                model = $"models/{embeddingModel}",
                content = new
                {
                    parts = new[]
                    {
                        new { text }
                    }
                }
            };

            var jsonBody = JsonSerializer.Serialize(requestPayload);
            using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(requestUrl, content, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google Gemini embedding API returned status {StatusCode}. Using fallback.", response.StatusCode);
                return GenerateDeterministicEmbedding(text, 768);
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("embedding", out var embeddingProp) &&
                embeddingProp.TryGetProperty("values", out var valuesProp))
            {
                var list = new List<float>();
                foreach (var element in valuesProp.EnumerateArray())
                {
                    list.Add(element.GetSingle());
                }
                return list.ToArray();
            }

            return GenerateDeterministicEmbedding(text, 768);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Gemini embedding request failed. Generating fallback vector representation.");
            return GenerateDeterministicEmbedding(text, 768);
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

        norm = (float)Math.Sqrt(norm);
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
