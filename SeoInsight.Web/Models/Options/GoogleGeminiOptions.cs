using System.ComponentModel.DataAnnotations;

namespace SeoInsight.Web.Models.Options;

/// <summary>
/// Strongly-typed options for Google Gemini API.
/// </summary>
public sealed class GoogleGeminiOptions
{
    public const string SectionName = "GoogleGemini";

    public string ApiKey { get; set; } = string.Empty;

    public string ChatModel { get; set; } = "gemini-3.5-flash-lite";

    [Range(0.0, 2.0)]
    public double Temperature { get; set; } = 0.2;

    [Range(100, 32000)]
    public int MaxOutputTokens { get; set; } = 4000;
}
