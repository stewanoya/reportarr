using JellyfinReporter.MediaManager;
using Xunit;

namespace Reportarr.UnitTests.MediaManager;

public class QueueSnapshotBuilderTests
{
    private static QueueItem MakeItem(int id, string title, string status, decimal size, decimal sizeLeft,
        string? downloadId = null, string? trackedStatus = null) => new()
    {
        Id = id,
        Title = title,
        Status = status,
        Size = size,
        SizeLeft = sizeLeft,
        DownloadId = downloadId ?? $"dl-{id}",
        TrackedDownloadStatus = trackedStatus
    };

    [Fact]
    public void Build_EmptyItems_ProducesEmptySnapshot()
    {
        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, [], new QueueStateCache(), DateTime.UtcNow);

        Assert.Empty(snapshot.Rows);
        Assert.Equal(0, snapshot.TotalCount);
        Assert.Equal(0, snapshot.DownloadingCount);
        Assert.False(snapshot.IsOffline);
    }

    [Fact]
    public void Build_SonarrItemWithSeriesAndEpisode_UsesSeriesEpisodeTitle()
    {
        var item = MakeItem(1, "Some.Release.Group", "Downloading", 1000m, 500m);
        item.Series = new SeriesSummary { Title = "The Show" };
        item.Episode = new EpisodeSummary { EpisodeNumber = 3, Title = "Pilot" };
        item.SeasonNumber = 1;

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, [item], new QueueStateCache(), DateTime.UtcNow);

        Assert.Equal("The Show S01E03", snapshot.Rows[0].DisplayTitle);
    }

    [Fact]
    public void Build_SonarrItemWithSeriesOnly_FallsBackToSeriesTitle()
    {
        var item = MakeItem(1, "Some.Release.Group", "Downloading", 1000m, 500m);
        item.Series = new SeriesSummary { Title = "The Show" };
        // no episode

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, [item], new QueueStateCache(), DateTime.UtcNow);

        Assert.Equal("The Show", snapshot.Rows[0].DisplayTitle);
    }

    [Fact]
    public void Build_SonarrItemWithoutSeries_FallsBackToReleaseTitle()
    {
        var item = MakeItem(1, "Some.Release.Group", "Downloading", 1000m, 500m);

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, [item], new QueueStateCache(), DateTime.UtcNow);

        Assert.Equal("Some.Release.Group", snapshot.Rows[0].DisplayTitle);
    }

    [Fact]
    public void Build_RadarrItemWithMovieAndYear_UsesMovieTitleWithYear()
    {
        var item = MakeItem(1, "Some.Release.Group", "Downloading", 1000m, 500m);
        item.Movie = new MovieSummary { Title = "The Movie", Year = 2024 };

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Radarr, [item], new QueueStateCache(), DateTime.UtcNow);

        Assert.Equal("The Movie (2024)", snapshot.Rows[0].DisplayTitle);
    }

    [Fact]
    public void Build_RadarrItemWithMovieNoYear_UsesMovieTitleOnly()
    {
        var item = MakeItem(1, "Some.Release.Group", "Downloading", 1000m, 500m);
        item.Movie = new MovieSummary { Title = "The Movie", Year = 0 };

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Radarr, [item], new QueueStateCache(), DateTime.UtcNow);

        Assert.Equal("The Movie", snapshot.Rows[0].DisplayTitle);
    }

    [Fact]
    public void Build_RadarrItemWithoutMovie_FallsBackToReleaseTitle()
    {
        var item = MakeItem(1, "Some.Release.Group", "Downloading", 1000m, 500m);

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Radarr, [item], new QueueStateCache(), DateTime.UtcNow);

        Assert.Equal("Some.Release.Group", snapshot.Rows[0].DisplayTitle);
    }

    [Fact]
    public void Build_ProgressIsComputedFromSizeAndSizeLeft()
    {
        var item = MakeItem(1, "title", "Downloading", size: 1000m, sizeLeft: 750m);

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, [item], new QueueStateCache(), DateTime.UtcNow);

        Assert.Equal(25, snapshot.Rows[0].ProgressPercent);
    }

    [Fact]
    public void Build_ProgressClampedToZeroWhenSizeIsZero()
    {
        var item = MakeItem(1, "title", "Downloading", size: 0m, sizeLeft: 0m);

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, [item], new QueueStateCache(), DateTime.UtcNow);

        Assert.Equal(0, snapshot.Rows[0].ProgressPercent);
    }

    [Fact]
    public void Build_ProgressClampedToRangeWhenSizeLeftExceedsSize()
    {
        // Defensive: API shouldn't emit this, but don't show >100% or <0%.
        var item = MakeItem(1, "title", "Downloading", size: 1000m, sizeLeft: 1500m);

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, [item], new QueueStateCache(), DateTime.UtcNow);

        Assert.Equal(0, snapshot.Rows[0].ProgressPercent);
    }

    [Fact]
    public void Build_AggregatesStatusCountsCorrectly()
    {
        var items = new[]
        {
            MakeItem(1, "a", "Downloading", 1000m, 500m),
            MakeItem(2, "b", "Downloading", 1000m, 500m),
            MakeItem(3, "c", "Queued", 1000m, 1000m),
            MakeItem(4, "d", "Paused", 1000m, 1000m),
            MakeItem(5, "e", "Delay", 1000m, 1000m),
            MakeItem(6, "f", "Downloading", 1000m, 500m, trackedStatus: "Warning"),
            MakeItem(7, "g", "Failed", 1000m, 1000m),
            MakeItem(8, "h", "Downloading", 1000m, 500m, trackedStatus: "Error"),
        };

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, items, new QueueStateCache(), DateTime.UtcNow);

        Assert.Equal(8, snapshot.TotalCount);
        Assert.Equal(4, snapshot.DownloadingCount);
        Assert.Equal(1, snapshot.QueuedCount);
        Assert.Equal(2, snapshot.PausedCount); // Paused + Delay
        Assert.Equal(1, snapshot.WarningCount);
        Assert.Equal(2, snapshot.ErrorCount); // Failed status + Error tracked
    }

    [Fact]
    public void Build_ErrorTrackedStatusFlagsIsError()
    {
        var item = MakeItem(1, "t", "Downloading", 1000m, 500m, trackedStatus: "Error");

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, [item], new QueueStateCache(), DateTime.UtcNow);

        Assert.True(snapshot.Rows[0].IsError);
        Assert.Equal(1, snapshot.ErrorCount);
    }

    [Fact]
    public void Build_FailedStatusFlagsIsError()
    {
        var item = MakeItem(1, "t", "Failed", 1000m, 1000m);

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, [item], new QueueStateCache(), DateTime.UtcNow);

        Assert.True(snapshot.Rows[0].IsError);
    }

    [Fact]
    public void Build_WarningTrackedStatusFlagsIsWarning()
    {
        var item = MakeItem(1, "t", "Downloading", 1000m, 500m, trackedStatus: "Warning");

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, [item], new QueueStateCache(), DateTime.UtcNow);

        Assert.True(snapshot.Rows[0].IsWarning);
        Assert.Equal(1, snapshot.WarningCount);
    }

    [Fact]
    public void Build_ErrorMessageSurfacesInStatusMessageText()
    {
        var item = MakeItem(1, "t", "Downloading", 1000m, 500m);
        item.ErrorMessage = "client rejected the download";

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, [item], new QueueStateCache(), DateTime.UtcNow);

        Assert.Equal("client rejected the download", snapshot.Rows[0].StatusMessageText);
    }

    [Fact]
    public void Build_StatusMessagesAreJoinedWithPipe()
    {
        var item = MakeItem(1, "t", "Downloading", 1000m, 500m);
        item.StatusMessages =
        [
            new StatusMessage { Title = "Import", Messages = ["file locked", "permission denied"] },
            new StatusMessage { Title = "Other" }
        ];

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, [item], new QueueStateCache(), DateTime.UtcNow);

        Assert.Equal("file locked | permission denied | Other", snapshot.Rows[0].StatusMessageText);
    }

    [Fact]
    public void Build_StatusMessageWithOnlyTitleUsesTitle()
    {
        var item = MakeItem(1, "t", "Downloading", 1000m, 500m);
        item.StatusMessages = [new StatusMessage { Title = "Stuck import" }];

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, [item], new QueueStateCache(), DateTime.UtcNow);

        Assert.Equal("Stuck import", snapshot.Rows[0].StatusMessageText);
    }

    [Fact]
    public void Build_DownloadingItemWithNoPriorSpeed_ReturnsNullSpeed()
    {
        var item = MakeItem(1, "t", "Downloading", 1000m, 500m);

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, [item], new QueueStateCache(), DateTime.UtcNow);

        Assert.Null(snapshot.Rows[0].SpeedBytesPerSec);
    }

    [Fact]
    public void Build_DownloadingItemWithPriorSpeed_ReturnsDerivedSpeed()
    {
        var cache = new QueueStateCache();
        var t0 = DateTime.UtcNow;
        cache.UpdateAndGetSpeed("dl-1", 1000m, t0, isDownloading: true);

        var item = MakeItem(1, "t", "Downloading", 1000m, 500m, downloadId: "dl-1");

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, [item], cache, t0.AddSeconds(10));

        Assert.NotNull(snapshot.Rows[0].SpeedBytesPerSec);
        Assert.Equal(50, snapshot.Rows[0].SpeedBytesPerSec);
    }

    [Fact]
    public void Build_NonDownloadingItem_HasNullSpeed()
    {
        var cache = new QueueStateCache();
        var t0 = DateTime.UtcNow;
        cache.UpdateAndGetSpeed("dl-1", 1000m, t0, isDownloading: true);

        var item = MakeItem(1, "t", "Queued", 1000m, 1000m, downloadId: "dl-1");

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, [item], cache, t0.AddSeconds(10));

        Assert.Null(snapshot.Rows[0].SpeedBytesPerSec);
        Assert.False(snapshot.Rows[0].IsDownloading);
    }

    [Fact]
    public void Build_CarriesQueueItemIdToRow()
    {
        var item = MakeItem(42, "t", "Downloading", 1000m, 500m);

        var snapshot = QueueSnapshotBuilder.Build(ArrServiceKind.Sonarr, [item], new QueueStateCache(), DateTime.UtcNow);

        Assert.Equal(42, snapshot.Rows[0].Id);
    }
}
