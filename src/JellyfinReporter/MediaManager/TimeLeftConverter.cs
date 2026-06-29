using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JellyfinReporter.MediaManager;

/// <summary>
/// Parses the 'timeleft' field from Sonarr/Radarr's queue API.
/// The API may emit either "HH:MM:SS" / "HH:MM:SS.fffffff" or an ISO 8601
/// duration ("PT1H30M"). This converter handles both.
/// </summary>
public sealed class TimeLeftConverter : JsonConverter<TimeSpan?>
{
    public override TimeSpan? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return ParseTimeLeft(value);
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(value.Value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture));
        else
            writer.WriteNullValue();
    }

    private static TimeSpan? ParseTimeLeft(string value)
    {
        // ISO 8601 duration: PT#H#M#S — parsed by XmlConvert, not TimeSpan.TryParse
        if (value.StartsWith("PT", StringComparison.OrdinalIgnoreCase) ||
            (value.StartsWith('-') && value.AsSpan(1).StartsWith("PT", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                return System.Xml.XmlConvert.ToTimeSpan(value);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        // HH:MM:SS or HH:MM:SS.fffffff
        if (TimeSpan.TryParseExact(value, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var hms))
            return hms;
        if (TimeSpan.TryParseExact(value, @"hh\:mm\:ss\.fffffff", CultureInfo.InvariantCulture, out var hmsf))
            return hmsf;

        // Last-resort fallback: let the runtime try
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var fallback))
            return fallback;

        return null;
    }
}
