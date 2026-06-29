using JellyfinReporter.Discord;
using JellyfinReporter.MediaManager;
using Xunit;

namespace Reportarr.UnitTests.Discord;

public class QueueTemplateHelperTests
{
    private static QueueSnapshot MakeSnapshot(ArrServiceKind kind, params QueueRow[] rows) => new()
    {
        Kind = kind,
        Rows = rows,
        TotalCount = rows.Length,
        DownloadingCount = rows.Count(r => r.IsDownloading),
        QueuedCount = rows.Count(r => r.Status.Equals("Queued", StringComparison.OrdinalIgnoreCase)),
        PausedCount = rows.Count(r => r.Status.Equals("Paused", StringComparison.OrdinalIgnoreCase)
                                      || r.Status.Equals("Delay", StringComparison.OrdinalIgnoreCase)),
        WarningCount = rows.Count(r => r.IsWarning),
        ErrorCount = rows.Count(r => r.IsError)
    };

    private static QueueRow MakeRow(string title, string status, double progress, decimal size,
        long? speed = null, TimeSpan? timeLeft = null, bool isError = false, bool isWarning = false) => new()
    {
        DisplayTitle = title,
        Status = status,
        ProgressPercent = progress,
        SizeBytes = size,
        SpeedBytesPerSec = speed,
        TimeLeft = timeLeft,
        IsError = isError,
        IsWarning = isWarning,
        IsDownloading = status.Equals("Downloading", StringComparison.OrdinalIgnoreCase)
    };

    // ---- Header ----

    [Theory]
    [InlineData(ArrServiceKind.Sonarr, "📺 Sonarr Queue")]
    [InlineData(ArrServiceKind.Radarr, "🎬 Radarr Queue")]
    public void Header_ReturnsExpectedPerKind(ArrServiceKind kind, string expected)
    {
        Assert.Equal(expected, QueueTemplateHelper.Header(kind));
    }

    // ---- Pinned message rendering ----

    [Fact]
    public void RenderPinnedMessage_OfflineSnapshot_RendersOfflineBanner()
    {
        var snapshot = QueueSnapshot.Offline(ArrServiceKind.Sonarr, "unreachable");

        var result = QueueTemplateHelper.RenderPinnedMessage(snapshot);

        Assert.Contains("📺 Sonarr Queue", result);
        Assert.Contains("🔴 OFFLINE — unreachable", result);
        Assert.DoesNotContain("||", result);
    }

    [Fact]
    public void RenderPinnedMessage_EmptyQueue_RendersIdleBanner()
    {
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr);

        var result = QueueTemplateHelper.RenderPinnedMessage(snapshot);

        Assert.Contains("📺 Sonarr Queue", result);
        Assert.Contains("Queue empty", result);
        Assert.Contains("✅ No active downloads", result);
        Assert.DoesNotContain("||", result);
    }

    [Fact]
    public void RenderPinnedMessage_ActiveQueue_WrapsTableInSpoilerBlock()
    {
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr,
            MakeRow("The Show S01E01", "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(5)));

        var result = QueueTemplateHelper.RenderPinnedMessage(snapshot);

        Assert.Contains("||", result);
        Assert.Contains("```", result);
        Assert.Contains("The Show S01E01", result);
        Assert.Contains("1 downloading", result);
    }

    [Fact]
    public void RenderPinnedMessage_SummaryLineAggregatesCounts()
    {
        var snapshot = new QueueSnapshot
        {
            Kind = ArrServiceKind.Radarr,
            Rows = [],
            TotalCount = 0,
            DownloadingCount = 3,
            QueuedCount = 1,
            PausedCount = 2,
            WarningCount = 1,
            ErrorCount = 2
        };

        var result = QueueTemplateHelper.RenderPinnedMessage(snapshot);

        Assert.Contains("3 downloading · 1 queued · 2 paused · ⚠️ 1 warning · 🔴 2 errors", result);
    }

    [Fact]
    public void RenderPinnedMessage_ErrorRowHasRedEmoji()
    {
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr,
            MakeRow("Bad Download", "Failed", 0, 1000m, isError: true));

        var result = QueueTemplateHelper.RenderPinnedMessage(snapshot);

        Assert.Contains("🔴", result);
    }

    [Fact]
    public void RenderPinnedMessage_WarningRowHasWarningEmoji()
    {
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr,
            MakeRow("Slow Download", "Downloading", 50, 1000m, isWarning: true));

        var result = QueueTemplateHelper.RenderPinnedMessage(snapshot);

        Assert.Contains("⚠️", result);
    }

    [Fact]
    public void RenderPinnedMessage_CapsAtTenRowsAndNotesRemainder()
    {
        var rows = Enumerable.Range(1, 13)
            .Select(i => MakeRow($"Title{i:D2}", "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(i)))
            .ToArray();
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr, rows);

        var result = QueueTemplateHelper.RenderPinnedMessage(snapshot);

        Assert.Contains("...and 3 more — click 📥 for full list", result);
    }

    [Fact]
    public void RenderPinnedMessage_DownloadingSortedBeforeQueued()
    {
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr,
            MakeRow("Queued Show", "Queued", 0, 1000m, timeLeft: TimeSpan.FromMinutes(1)),
            MakeRow("Active Show", "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(5)));

        var result = QueueTemplateHelper.RenderPinnedMessage(snapshot);

        var activePos = result.IndexOf("Active Show", StringComparison.Ordinal);
        var queuedPos = result.IndexOf("Queued Show", StringComparison.Ordinal);
        Assert.True(activePos < queuedPos, "Downloading items should appear before queued items");
    }

    // ---- Offline rendering ----

    [Fact]
    public void RenderOffline_IncludesKindHeaderAndReason()
    {
        var result = QueueTemplateHelper.RenderOffline(ArrServiceKind.Radarr, "bad API key (401)");

        Assert.Contains("🎬 Radarr Queue", result);
        Assert.Contains("🔴 OFFLINE — bad API key (401)", result);
    }

    // ---- Full list chunking ----

    [Fact]
    public void RenderFullListChunks_Offline_ReturnsSingleOfflineMessage()
    {
        var snapshot = QueueSnapshot.Offline(ArrServiceKind.Sonarr, "timeout");

        var chunks = QueueTemplateHelper.RenderFullListChunks(snapshot).ToList();

        Assert.Single(chunks);
        Assert.Contains("🔴 OFFLINE — timeout", chunks[0]);
    }

    [Fact]
    public void RenderFullListChunks_EmptyQueue_ReturnsSingleIdleMessage()
    {
        var snapshot = MakeSnapshot(ArrServiceKind.Radarr);

        var chunks = QueueTemplateHelper.RenderFullListChunks(snapshot).ToList();

        Assert.Single(chunks);
        Assert.Contains("✅ No active downloads", chunks[0]);
    }

    [Fact]
    public void RenderFullListChunks_AllItemsAppearEvenWhenMany()
    {
        var rows = Enumerable.Range(1, 50)
            .Select(i => MakeRow($"Title{i:D3}", "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(i)))
            .ToArray();
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr, rows);

        var chunks = QueueTemplateHelper.RenderFullListChunks(snapshot).ToList();

        Assert.NotEmpty(chunks);
        var combined = string.Join("\n", chunks);
        for (var i = 1; i <= 50; i++)
            Assert.Contains($"Title{i:D3}", combined);
    }

    [Fact]
    public void RenderFullListChunks_EachChunkUnderCharLimit()
    {
        var rows = Enumerable.Range(1, 100)
            .Select(i => MakeRow(new string('A', 60), "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(i)))
            .ToArray();
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr, rows);

        var chunks = QueueTemplateHelper.RenderFullListChunks(snapshot).ToList();

        Assert.All(chunks, chunk => Assert.True(chunk.Length <= 2000, $"chunk was {chunk.Length} chars"));
    }

    [Fact]
    public void RenderFullListChunks_ContinuationChunksHaveMarker()
    {
        var rows = Enumerable.Range(1, 50)
            .Select(i => MakeRow(new string('A', 60), "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(i)))
            .ToArray();
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr, rows);

        var chunks = QueueTemplateHelper.RenderFullListChunks(snapshot).ToList();

        Assert.True(chunks.Count >= 2, "expected at least 2 chunks");
        Assert.All(chunks.Skip(1), chunk => Assert.Contains("continued", chunk));
    }

    // ---- DM button custom id ----

    [Theory]
    [InlineData(ArrServiceKind.Sonarr, "queue_dm_sonarr")]
    [InlineData(ArrServiceKind.Radarr, "queue_dm_radarr")]
    public void DmButtonCustomId_ReturnsExpectedPerKind(ArrServiceKind kind, string expected)
    {
        Assert.Equal(expected, QueueTemplateHelper.DmButtonCustomId(kind));
    }

    // ---- Formatters ----

    [Theory]
    [InlineData(0L, "0.0B/s")]
    [InlineData(512L, "512.0B/s")]
    [InlineData(1024L, "1.0KB/s")]
    [InlineData(1_048_576L, "1.0MB/s")]
    [InlineData(1_073_741_824L, "1.0GB/s")]
    public void FormatSpeed_ScalesIntoAppropriateUnit(long bytesPerSec, string expectedSubstring)
    {
        var result = QueueTemplateHelper.FormatSpeed(bytesPerSec);
        // Trim leading spaces since the test substrings above show the alignment padding.
        Assert.Equal(expectedSubstring.Trim(), result.Trim());
    }

    [Theory]
    [InlineData(0.0, "0.0B")]
    [InlineData(1024.0, "1.0KB")]
    [InlineData(1572864.0, "1.5MB")]
    [InlineData(1073741824.0, "1.0GB")]
    public void FormatBytes_ScalesIntoAppropriateUnit(double bytes, string expectedSubstring)
    {
        var result = QueueTemplateHelper.FormatBytes((decimal)bytes);
        Assert.Equal(expectedSubstring.Trim(), result.Trim());
    }

    [Fact]
    public void FormatEta_TimeLeftHoursAndMinutes_RendersCompactForm()
    {
        var result = QueueTemplateHelper.FormatEta(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(30)), null);
        Assert.Equal("2h30m", result);
    }

    [Fact]
    public void FormatEta_TimeLeftMinutesOnly_RendersMinutesSeconds()
    {
        var result = QueueTemplateHelper.FormatEta(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(15)), null);
        Assert.Equal("5m15s", result);
    }

    [Fact]
    public void FormatEta_TimeLeftSecondsOnly_RendersSeconds()
    {
        var result = QueueTemplateHelper.FormatEta(TimeSpan.FromSeconds(8), null);
        Assert.Equal("8s", result);
    }

    [Fact]
    public void FormatEta_NullTimeLeftAndNullEta_ReturnsDash()
    {
        var result = QueueTemplateHelper.FormatEta(null, null);
        Assert.Equal("—", result);
    }

    [Fact]
    public void FormatEta_EtaInThePast_ReturnsNow()
    {
        var result = QueueTemplateHelper.FormatEta(null, DateTime.UtcNow.AddMinutes(-5));
        Assert.Equal("now", result);
    }

    [Fact]
    public void FormatEta_EtaInFuture_FallsBackWhenTimeLeftNull()
    {
        var result = QueueTemplateHelper.FormatEta(null, DateTime.UtcNow.AddMinutes(10));
        Assert.Contains("m", result);
    }
}
