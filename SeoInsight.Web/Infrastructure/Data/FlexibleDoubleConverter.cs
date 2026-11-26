using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeoInsight.Web.Infrastructure.Data;

/// <summary>
/// A flexible converter that can deserialize double values from numbers, quoted strings (e.g. "14.0", "100+", "N/A"), or null.
/// When non-numeric string like "100+" or "N/A" is encountered, it safely falls back to a sentinel value (e.g. 101.0).
/// </summary>
public sealed class FlexibleDoubleConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDouble();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return 0.0;
            }

            if (double.TryParse(stringValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            // If it's something like "Above 50", ">50", "50+", "100+", "unranked"
            if (stringValue.Contains("Above", StringComparison.OrdinalIgnoreCase) ||
                stringValue.Contains(">") ||
                stringValue.Contains("100") ||
                stringValue.Contains("+") ||
                stringValue.Contains("unranked", StringComparison.OrdinalIgnoreCase))
            {
                return 51.0;
            }

            // Handle patterns like "#14"
            var digitsOnly = System.Text.RegularExpressions.Regex.Match(stringValue, @"\d+(\.\d+)?");
            if (digitsOnly.Success && double.TryParse(digitsOnly.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
            {
                return num;
            }

            return 0.0;
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return 0.0;
        }

        return 0.0;
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}
