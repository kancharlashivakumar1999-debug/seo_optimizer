using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeoInsight.Web.Infrastructure.Data;

/// <summary>
/// A flexible converter that deserializes a position as a clean display string (e.g. "50+", "14.0", "3").
/// Accepts numbers, numeric strings, or unranked representations ("50+", "Above 50", etc.)
/// and serializes back as a string or number cleanly.
/// </summary>
public sealed class FlexiblePositionStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetDouble(out var d))
            {
                return d > 50 ? "50+" : d.ToString("0.#");
            }
            return reader.GetString() ?? "50+";
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(str))
            {
                return "50+";
            }

            if (str.Contains("50") || str.Contains("+") || str.Contains("Above", StringComparison.OrdinalIgnoreCase) || str.Contains("unranked", StringComparison.OrdinalIgnoreCase))
            {
                return "50+";
            }

            if (double.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed > 50 ? "50+" : parsed.ToString("0.#");
            }

            return str;
        }

        return "50+";
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
        {
            writer.WriteNumberValue(num);
        }
        else
        {
            writer.WriteStringValue(value ?? "50+");
        }
    }
}
