using JellyfinReporter.Discord;
using JellyfinReporter.MediaManager;
using NetCord.Rest;
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
        long? speed = null, TimeSpan? timeLeft = null, bool isError = false, bool isWarning = false,
        string? statusMessageText = null, int id = 0) => new()
    {
        Id = id,
        DisplayTitle = title,
        Status = status,
        ProgressPercent = progress,
        SizeBytes = size,
        SpeedBytesPerSec = speed,
        TimeLeft = timeLeft,
        IsError = isError,
        IsWarning = isWarning,
        IsDownloading = status.Equals("Downloading", StringComparison.OrdinalIgnoreCase),
        StatusMessageText = statusMessageText
    };

    // ---- Header ----

    [Theory]
    [InlineData(ArrServiceKind.Sonarr, "📺 TV Shows Queue")]
    [InlineData(ArrServiceKind.Radarr, "🎬 Movies Queue")]
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

        Assert.Contains("📺 TV Shows Queue", result);
        Assert.Contains("🔴 OFFLINE — unreachable", result);
        Assert.DoesNotContain("||", result);
        Assert.DoesNotContain("```", result);
    }

    [Fact]
    public void RenderPinnedMessage_EmptyQueue_RendersIdleBanner()
    {
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr);

        var result = QueueTemplateHelper.RenderPinnedMessage(snapshot);

        Assert.Contains("📺 TV Shows Queue", result);
        Assert.Contains("Queue empty", result);
        Assert.Contains("✅ No active downloads", result);
        Assert.DoesNotContain("||", result);
        Assert.DoesNotContain("```", result);
    }

    [Fact]
    public void RenderPinnedMessage_ActiveQueue_RendersListWithoutSpoilerOrCodeblock()
    {
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr,
            MakeRow("The Show S01E01", "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(5)));

        var result = QueueTemplateHelper.RenderPinnedMessage(snapshot);

        Assert.DoesNotContain("||", result);
        Assert.DoesNotContain("```", result);
        Assert.Contains("The Show S01E01", result);
        Assert.Contains("1 downloading", result);
        Assert.Contains("**", result); // title is bolded
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
    public void RenderPinnedMessage_CapsAtFiveRowsAndNotesRemainder()
    {
        var rows = Enumerable.Range(1, 8)
            .Select(i => MakeRow($"Title{i:D2}", "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(i)))
            .ToArray();
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr, rows);

        var result = QueueTemplateHelper.RenderPinnedMessage(snapshot);

        Assert.Contains("…and 3 more — click 📥 for full list", result);
        // Should contain exactly 5 row lines (Title01..Title05)
        Assert.Contains("Title01", result);
        Assert.Contains("Title05", result);
        Assert.DoesNotContain("Title06", result);
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

    [Fact]
    public void RenderPinnedMessage_RowContainsProgressSpeedEta()
    {
        var snapshot = MakeSnapshot(ArrServiceKind.Radarr,
            MakeRow("The Movie", "Downloading", 42, 1_073_741_824m, speed: 5_242_880, timeLeft: TimeSpan.FromMinutes(3)));

        var result = QueueTemplateHelper.RenderPinnedMessage(snapshot);

        Assert.Contains("42%", result);
        Assert.Contains("5.0MB/s", result);
        Assert.Contains("3m", result);
        Assert.DoesNotContain("1.0GB", result); // size dropped from rows
    }

    [Fact]
    public void RenderPinnedMessage_StalledStatusShowsPauseIcon()
    {
        // A status not explicitly mapped (e.g. "Stalled") falls to the default ⏸️ icon.
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr,
            MakeRow("Stalled Show", "Stalled", 50, 1000m));

        var result = QueueTemplateHelper.RenderPinnedMessage(snapshot);

        Assert.Contains("⏸️", result);
        Assert.DoesNotContain("•", result);
    }

    [Fact]
    public void RenderPinnedMessage_WarningRowShowsWarningMessage()
    {
        var row = MakeRow("Slow Show", "Downloading", 50, 1000m, isWarning: true, statusMessageText: "import blocked");
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr, row);

        var result = QueueTemplateHelper.RenderPinnedMessage(snapshot);

        Assert.Contains("⚠️", result);
        Assert.Contains("import blocked", result);
    }

    [Fact]
    public void RenderPinnedMessage_ErrorRowShowsErrorMessage()
    {
        var row = MakeRow("Broken Show", "Failed", 0, 1000m, isError: true, statusMessageText: "download failed");
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr, row);

        var result = QueueTemplateHelper.RenderPinnedMessage(snapshot);

        Assert.Contains("🔴", result);
        Assert.Contains("download failed", result);
    }

    [Fact]
    public void RenderPinnedMessage_HasTrailingSpaceForButtonSpacing()
    {
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr,
            MakeRow("Some Show", "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(5)));

        var result = QueueTemplateHelper.RenderPinnedMessage(snapshot);

        Assert.True(result.EndsWith("\n\n", StringComparison.Ordinal),
            $"expected trailing blank lines for button spacing, got: {result[^20..]}");
    }

    [Fact]
    public void BuildPinnedComponents_HasDeleteAndDmButton()
    {
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr,
            MakeRow("Some Show", "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(5)));

        var components = QueueTemplateHelper.BuildPinnedComponents(snapshot);

        Assert.Single(components); // one button row
    }

    // ---- Delete button custom IDs ----

    [Theory]
    [InlineData(ArrServiceKind.Sonarr, "queue_del_sonarr")]
    [InlineData(ArrServiceKind.Radarr, "queue_del_radarr")]
    public void DeleteButtonCustomId_ReturnsExpectedFormat(ArrServiceKind kind, string expected)
    {
        Assert.Equal(expected, QueueTemplateHelper.DeleteButtonCustomId(kind));
    }

    [Theory]
    [InlineData(ArrServiceKind.Sonarr, 1, "queue_page_sonarr:1")]
    [InlineData(ArrServiceKind.Radarr, 3, "queue_page_radarr:3")]
    public void PageButtonCustomId_ReturnsExpectedFormat(ArrServiceKind kind, int page, string expected)
    {
        Assert.Equal(expected, QueueTemplateHelper.PageButtonCustomId(kind, page));
    }

    [Theory]
    [InlineData(ArrServiceKind.Sonarr, "queue_sel_sonarr")]
    [InlineData(ArrServiceKind.Radarr, "queue_sel_radarr")]
    public void SelectMenuCustomId_ReturnsExpectedFormat(ArrServiceKind kind, string expected)
    {
        Assert.Equal(expected, QueueTemplateHelper.SelectMenuCustomId(kind));
    }

    [Theory]
    [InlineData(ArrServiceKind.Sonarr, "queue_rev_sonarr")]
    [InlineData(ArrServiceKind.Radarr, "queue_rev_radarr")]
    public void ReviewButtonCustomId_ReturnsExpectedFormat(ArrServiceKind kind, string expected)
    {
        Assert.Equal(expected, QueueTemplateHelper.ReviewButtonCustomId(kind));
    }

    [Theory]
    [InlineData(ArrServiceKind.Sonarr, "queue_delc_sonarr")]
    [InlineData(ArrServiceKind.Radarr, "queue_delc_radarr")]
    public void ConfirmDeleteCustomId_ReturnsExpectedFormat(ArrServiceKind kind, string expected)
    {
        Assert.Equal(expected, QueueTemplateHelper.ConfirmDeleteCustomId(kind));
    }

    [Theory]
    [InlineData("queue_del_sonarr", ArrServiceKind.Sonarr)]
    [InlineData("queue_del_radarr", ArrServiceKind.Radarr)]
    [InlineData("queue_sel_sonarr", ArrServiceKind.Sonarr)]
    [InlineData("queue_delc_radarr", ArrServiceKind.Radarr)]
    public void TryParseKind_ValidId_ReturnsKind(string customId, ArrServiceKind expectedKind)
    {
        var ok = QueueTemplateHelper.TryParseKind(customId, out var kind);

        Assert.True(ok);
        Assert.Equal(expectedKind, kind);
    }

    [Theory]
    [InlineData("queue_delx")]
    [InlineData("")]
    [InlineData("no_underscore")]
    public void TryParseKind_InvalidId_ReturnsFalse(string customId)
    {
        var ok = QueueTemplateHelper.TryParseKind(customId, out _);
        Assert.False(ok);
    }

    // ---- BuildPinnedComponents ----

    [Fact]
    public void BuildPinnedComponents_WithItems_HasDeleteAndDmButton()
    {
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr,
            MakeRow("Show A", "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(5), id: 1),
            MakeRow("Show B", "Downloading", 30, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(10), id: 2));

        var components = QueueTemplateHelper.BuildPinnedComponents(snapshot);

        Assert.Single(components); // one button row
    }

    // ---- BuildPagedSelectComponents ----

    [Fact]
    public void BuildPagedSelectComponents_WithItems_ReturnsMenuAndNavRow()
    {
        var rows = new List<QueueRow>
        {
            MakeRow("Show A", "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(5), id: 10),
            MakeRow("Show B", "Downloading", 30, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(10), id: 20)
        };

        var components = QueueTemplateHelper.BuildPagedSelectComponents(
            ArrServiceKind.Sonarr, rows, page: 1, selectedIds: []);

        Assert.Equal(2, components.Count); // menu row + nav row
    }

    [Fact]
    public void BuildPagedSelectComponents_NoItems_ReturnsOnlyNavRow()
    {
        var components = QueueTemplateHelper.BuildPagedSelectComponents(
            ArrServiceKind.Sonarr, [], page: 1, selectedIds: []);

        Assert.Single(components); // only nav row
    }

    [Fact]
    public void BuildPagedSelectComponents_SinglePage_DisablesNavButtons()
    {
        var rows = Enumerable.Range(1, 5)
            .Select(i => MakeRow($"Title{i}", "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(i), id: i))
            .ToList();

        var components = QueueTemplateHelper.BuildPagedSelectComponents(
            ArrServiceKind.Sonarr, rows, page: 1, selectedIds: []);

        Assert.Equal(2, components.Count);
    }

    [Fact]
    public void BuildPagedSelectComponents_MultiplePages_EnablesNavButtons()
    {
        var rows = Enumerable.Range(1, 30)
            .Select(i => MakeRow($"Title{i}", "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(i), id: i))
            .ToList();

        var components = QueueTemplateHelper.BuildPagedSelectComponents(
            ArrServiceKind.Sonarr, rows, page: 1, selectedIds: []);

        Assert.Equal(2, components.Count); // menu + nav
    }

    [Fact]
    public void BuildPagedSelectComponents_PreSelectsChosenIds()
    {
        var rows = Enumerable.Range(1, 5)
            .Select(i => MakeRow($"Title{i}", "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(i), id: i))
            .ToList();

        var components = QueueTemplateHelper.BuildPagedSelectComponents(
            ArrServiceKind.Sonarr, rows, page: 1, selectedIds: [2, 4]);

        Assert.Equal(2, components.Count);
    }

    // ---- BuildConfirmComponents ----

    [Fact]
    public void BuildConfirmComponents_ReturnsConfirmBackCancelRow()
    {
        var components = QueueTemplateHelper.BuildConfirmComponents(ArrServiceKind.Sonarr);

        Assert.Single(components); // one action row
    }

    // ---- RenderSelectContent ----

    [Fact]
    public void RenderSelectContent_WithFewItems_NoPaginationHint()
    {
        var content = QueueTemplateHelper.RenderSelectContent(ArrServiceKind.Sonarr, totalItems: 5, selectedCount: 0);

        Assert.Contains("📺 TV Shows Queue", content);
        Assert.DoesNotContain("browse all pages", content);
    }

    [Fact]
    public void RenderSelectContent_WithManyItems_HasPaginationHint()
    {
        var content = QueueTemplateHelper.RenderSelectContent(ArrServiceKind.Sonarr, totalItems: 50, selectedCount: 0);

        Assert.Contains("browse all pages", content);
    }

    [Fact]
    public void RenderSelectContent_WithSelections_ShowsCount()
    {
        var content = QueueTemplateHelper.RenderSelectContent(ArrServiceKind.Sonarr, totalItems: 10, selectedCount: 3);

        Assert.Contains("3", content);
        Assert.Contains("selected", content);
    }

    // ---- RenderConfirmContent ----

    [Fact]
    public void RenderConfirmContent_ListsAllSelectedItems()
    {
        var items = new List<(int Id, string Title)>
        {
            (1, "Show A"),
            (2, "Show B"),
            (3, "Show C")
        };

        var content = QueueTemplateHelper.RenderConfirmContent(ArrServiceKind.Radarr, items);

        Assert.Contains("**3** item(s)", content);
        Assert.Contains("Show A", content);
        Assert.Contains("Show B", content);
        Assert.Contains("Show C", content);
    }

    // ---- Offline rendering ----

    [Fact]
    public void RenderOffline_IncludesKindHeaderAndReason()
    {
        var result = QueueTemplateHelper.RenderOffline(ArrServiceKind.Radarr, "bad API key (401)");

        Assert.Contains("🎬 Movies Queue", result);
        Assert.Contains("🔴 OFFLINE — bad API key (401)", result);
    }

    // ---- Full list chunking ----

    [Fact]
    public void RenderFullListMessages_Offline_ReturnsSingleOfflineMessage()
    {
        var snapshot = QueueSnapshot.Offline(ArrServiceKind.Sonarr, "timeout");

        var chunks = QueueTemplateHelper.RenderFullListMessages(snapshot).ToList();

        Assert.Single(chunks);
        Assert.Contains("🔴 OFFLINE — timeout", chunks[0].Content);
    }

    [Fact]
    public void RenderFullListMessages_EmptyQueue_ReturnsSingleIdleMessage()
    {
        var snapshot = MakeSnapshot(ArrServiceKind.Radarr);

        var chunks = QueueTemplateHelper.RenderFullListMessages(snapshot).ToList();

        Assert.Single(chunks);
        Assert.Contains("✅ No active downloads", chunks[0].Content);
    }

    [Fact]
    public void RenderFullListMessages_AllItemsAppearEvenWhenMany()
    {
        var rows = Enumerable.Range(1, 50)
            .Select(i => MakeRow($"Title{i:D3}", "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(i)))
            .ToArray();
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr, rows);

        var chunks = QueueTemplateHelper.RenderFullListMessages(snapshot).ToList();

        Assert.NotEmpty(chunks);
        var combined = string.Join("\n", chunks.Select(c => c.Content));
        for (var i = 1; i <= 50; i++)
            Assert.Contains($"Title{i:D3}", combined);
    }

    [Fact]
    public void RenderFullListMessages_EachChunkUnderCharLimit()
    {
        var rows = Enumerable.Range(1, 100)
            .Select(i => MakeRow(new string('A', 60), "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(i)))
            .ToArray();
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr, rows);

        var chunks = QueueTemplateHelper.RenderFullListMessages(snapshot).ToList();

        Assert.All(chunks, chunk => Assert.True(chunk.Content.Length <= 2000, $"chunk was {chunk.Content.Length} chars"));
    }

    [Fact]
    public void RenderFullListMessages_ContinuationChunksHaveMarker()
    {
        var rows = Enumerable.Range(1, 50)
            .Select(i => MakeRow(new string('A', 60), "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(i)))
            .ToArray();
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr, rows);

        var chunks = QueueTemplateHelper.RenderFullListMessages(snapshot).ToList();

        Assert.True(chunks.Count >= 2, "expected at least 2 chunks");
        Assert.All(chunks.Skip(1).Take(..^1), chunk => Assert.Contains("continued", chunk.Content));
    }

    [Fact]
    public void RenderFullListMessages_LegendIsLastChunkAndContainsAllIcons()
    {
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr,
            MakeRow("Some Show", "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(5)));

        var chunks = QueueTemplateHelper.RenderFullListMessages(snapshot).ToList();

        var last = chunks[^1].Content;
        Assert.Contains("📖 Legend", last);
        Assert.Contains("⬇️", last);
        Assert.Contains("⏳", last);
        Assert.Contains("⏸️", last);
        Assert.Contains("✅", last);
        Assert.Contains("❌", last);
        Assert.Contains("🔴", last);
        Assert.Contains("⚠️", last);
    }

    [Fact]
    public void RenderFullListMessages_ItemsHaveBlankLineBetweenThem()
    {
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr,
            MakeRow("First Show", "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(5)),
            MakeRow("Second Show", "Downloading", 30, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(10)));

        var chunks = QueueTemplateHelper.RenderFullListMessages(snapshot).ToList();

        var content = chunks[0].Content;
        var firstPos = content.IndexOf("First Show", StringComparison.Ordinal);
        var secondPos = content.IndexOf("Second Show", StringComparison.Ordinal);
        Assert.True(firstPos < secondPos);
        // There should be at least one blank line between the two items.
        var between = content.Substring(firstPos, secondPos - firstPos);
        Assert.Contains("\n\n", between);
    }

    [Fact]
    public void RenderFullListMessages_LastContentChunkHasDeleteButton()
    {
        var snapshot = MakeSnapshot(ArrServiceKind.Sonarr,
            MakeRow("Show A", "Downloading", 50, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(5), id: 10),
            MakeRow("Show B", "Downloading", 30, 1000m, speed: 100, timeLeft: TimeSpan.FromMinutes(10), id: 20));

        var chunks = QueueTemplateHelper.RenderFullListMessages(snapshot).ToList();

        // Last content chunk (before legend) should have delete button components.
        var lastContentChunk = chunks[^2];
        Assert.True(lastContentChunk.Components.Count > 0, "expected delete button on last content chunk");
        // Legend chunk should have no components.
        Assert.Empty(chunks[^1].Components);
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
    public void FormatSpeed_ScalesIntoAppropriateUnit(long bytesPerSec, string expected)
    {
        var result = QueueTemplateHelper.FormatSpeed(bytesPerSec);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0.0, "0.0B")]
    [InlineData(1024.0, "1.0KB")]
    [InlineData(1572864.0, "1.5MB")]
    [InlineData(1073741824.0, "1.0GB")]
    public void FormatBytes_ScalesIntoAppropriateUnit(double bytes, string expected)
    {
        var result = QueueTemplateHelper.FormatBytes((decimal)bytes);
        Assert.Equal(expected, result);
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
