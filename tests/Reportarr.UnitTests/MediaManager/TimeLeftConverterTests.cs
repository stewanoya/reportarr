using System.Text.Json;
using System.Text.Json.Serialization;
using JellyfinReporter.MediaManager;
using Xunit;

namespace Reportarr.UnitTests.MediaManager;

public class TimeLeftConverterTests
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static TimeSpan? ParseTimeLeft(string json)
    {
        var wrapped = $"{{\"timeleft\":{json}}}";
        var parsed = JsonSerializer.Deserialize<TestWrapper>(wrapped, _options);
        return parsed?.TimeLeft;
    }

    private sealed class TestWrapper
    {
        [JsonPropertyName("timeleft")]
        [JsonConverter(typeof(TimeLeftConverter))]
        public TimeSpan? TimeLeft { get; set; }
    }

    [Fact]
    public void Read_NullToken_ReturnsNull()
    {
        Assert.Null(ParseTimeLeft("null"));
    }

    [Fact]
    public void Read_EmptyString_ReturnsNull()
    {
        Assert.Null(ParseTimeLeft("\"\""));
    }

    [Fact]
    public void Read_HoursMinutesSeconds_ReturnsParsedTimespan()
    {
        var result = ParseTimeLeft("\"01:30:00\"");

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.FromHours(1.5), result);
    }

    [Fact]
    public void Read_ZeroTime_ReturnsZero()
    {
        var result = ParseTimeLeft("\"00:00:00\"");

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public void Read_Iso8601Duration_ReturnsParsedTimespan()
    {
        var result = ParseTimeLeft("\"PT1H30M\"");

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.FromHours(1.5), result);
    }

    [Fact]
    public void Read_Iso8601DurationWithSeconds_ReturnsParsedTimespan()
    {
        var result = ParseTimeLeft("\"PT2M15S\"");

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.FromSeconds(135), result);
    }

    [Fact]
    public void Read_GarbageString_ReturnsNull()
    {
        Assert.Null(ParseTimeLeft("\"not-a-duration\""));
    }

    [Fact]
    public void Read_NullJsonTokenInObject_ReturnsNull()
    {
        var json = "{\"timeleft\":null,\"other\":1}";
        var parsed = JsonSerializer.Deserialize<TestWrapper>(json, _options);

        Assert.Null(parsed?.TimeLeft);
    }

    [Fact]
    public void Roundtrip_WritesBackAsHHMMSS()
    {
        var wrapper = new TestWrapper { TimeLeft = TimeSpan.FromMinutes(90) };
        var json = JsonSerializer.Serialize(wrapper, _options);

        Assert.Contains("\"timeleft\":\"01:30:00\"", json);
    }

    [Fact]
    public void Roundtrip_NullWritesBackAsNull()
    {
        var wrapper = new TestWrapper { TimeLeft = null };
        var json = JsonSerializer.Serialize(wrapper, _options);

        Assert.Contains("\"timeleft\":null", json);
    }
}
