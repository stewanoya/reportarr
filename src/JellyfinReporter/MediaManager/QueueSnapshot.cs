namespace JellyfinReporter.MediaManager;

/// <summary>
/// A normalized, render-ready view of one service's queue at a point in time.
/// Decouples rendering from raw API DTOs.
/// </summary>
public sealed class QueueSnapshot
{
    public ArrServiceKind Kind { get; init; }
    public IReadOnlyList<QueueRow> Rows { get; init; } = [];
    public int TotalCount { get; init; }
    public int DownloadingCount { get; init; }
    public int QueuedCount { get; init; }
    public int PausedCount { get; init; }
    public int WarningCount { get; init; }
    public int ErrorCount { get; init; }
    public bool IsOffline { get; init; }
    public string? OfflineReason { get; init; }

    public static QueueSnapshot Offline(ArrServiceKind kind, string reason) => new()
    {
        Kind = kind,
        IsOffline = true,
        OfflineReason = reason
    };
}

public sealed class QueueRow
{
    public int Id { get; init; }
    public string DisplayTitle { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public double ProgressPercent { get; init; }
    public long? SpeedBytesPerSec { get; init; }
    public TimeSpan? TimeLeft { get; init; }
    public DateTime? EstimatedCompletionTime { get; init; }
    public decimal SizeBytes { get; init; }
    public bool IsError { get; init; }
    public bool IsWarning { get; init; }
    public string? StatusMessageText { get; init; }
    public bool IsDownloading { get; init; }
}
