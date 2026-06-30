namespace JellyfinReporter.MediaManager;

/// <summary>
/// Builds a <see cref="QueueSnapshot"/> from raw API <see cref="QueueItem"/>s,
/// resolving display titles and deriving download speed via <see cref="QueueStateCache"/>.
/// </summary>
public static class QueueSnapshotBuilder
{
    public static QueueSnapshot Build(
        ArrServiceKind kind,
        IReadOnlyList<QueueItem> items,
        QueueStateCache speedCache,
        DateTime now)
    {
        var rows = new List<QueueRow>(items.Count);
        var liveKeys = new List<string>(items.Count);

        int downloading = 0, queued = 0, paused = 0, warning = 0, error = 0;

        foreach (var item in items)
        {
            var key = item.DownloadId ?? item.Id.ToString();
            liveKeys.Add(key);

            var isDownloading = string.Equals(item.Status, "Downloading", StringComparison.OrdinalIgnoreCase);
            var isWarning = string.Equals(item.TrackedDownloadStatus, "Warning", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(item.Status, "Warning", StringComparison.OrdinalIgnoreCase);
            var isError = string.Equals(item.TrackedDownloadStatus, "Error", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(item.Status, "Failed", StringComparison.OrdinalIgnoreCase);

            if (isDownloading) downloading++;
            else if (string.Equals(item.Status, "Queued", StringComparison.OrdinalIgnoreCase)) queued++;
            else if (string.Equals(item.Status, "Paused", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(item.Status, "Delay", StringComparison.OrdinalIgnoreCase)) paused++;
            if (isWarning) warning++;
            if (isError) error++;

            var speed = speedCache.UpdateAndGetSpeed(key, item.SizeLeft, now, isDownloading);

            var progress = item.Size > 0
                ? Math.Max(0, Math.Min(100, 100 - (double)item.SizeLeft / (double)item.Size * 100))
                : 0;

            rows.Add(new QueueRow
            {
                Id = item.Id,
                DisplayTitle = ResolveDisplayTitle(kind, item),
                Status = item.Status,
                ProgressPercent = progress,
                SpeedBytesPerSec = speed,
                TimeLeft = item.TimeLeft,
                EstimatedCompletionTime = item.EstimatedCompletionTime,
                SizeBytes = item.Size,
                IsError = isError,
                IsWarning = isWarning,
                StatusMessageText = FormatStatusMessages(item),
                IsDownloading = isDownloading
            });
        }

        speedCache.PruneTo(liveKeys);

        return new QueueSnapshot
        {
            Kind = kind,
            Rows = rows,
            TotalCount = items.Count,
            DownloadingCount = downloading,
            QueuedCount = queued,
            PausedCount = paused,
            WarningCount = warning,
            ErrorCount = error
        };
    }

    private static string ResolveDisplayTitle(ArrServiceKind kind, QueueItem item)
    {
        if (kind == ArrServiceKind.Sonarr)
        {
            if (item.Series is { } series && item.Episode is { } episode && item.SeasonNumber is { } season)
                return $"{series.Title} S{season:00}E{episode.EpisodeNumber:00}";
            if (item.Series is { } s)
                return s.Title;
        }
        else if (kind == ArrServiceKind.Radarr)
        {
            if (item.Movie is { } movie)
                return movie.Year > 0 ? $"{movie.Title} ({movie.Year})" : movie.Title;
        }

        return item.Title;
    }

    private static string? FormatStatusMessages(QueueItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.ErrorMessage))
            return item.ErrorMessage;

        if (item.StatusMessages is null || item.StatusMessages.Count == 0)
            return null;

        var msgs = new List<string>();
        foreach (var sm in item.StatusMessages)
        {
            if (sm.Messages is { Count: > 0 })
                msgs.AddRange(sm.Messages);
            else if (!string.IsNullOrWhiteSpace(sm.Title))
                msgs.Add(sm.Title);
        }

        return msgs.Count > 0 ? string.Join(" | ", msgs) : null;
    }
}
