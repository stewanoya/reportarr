using System.Globalization;
using System.Text;
using JellyfinReporter.MediaManager;

namespace JellyfinReporter.Discord;

public static class QueueTemplateHelper
{
    private const string _sonarrHeader = "📺 Sonarr Queue";
    private const string _radarrHeader = "🎬 Radarr Queue";

    // Pinned spoiler table caps the visible rows; the DM button reveals the rest.
    private const int _pinnedRowCap = 10;
    private const int _titleTruncatePinned = 30;
    private const int _titleTruncateFull = 60;
    private const int _messageCharLimit = 1900; // safety margin under Discord's 2000-cap

    public static string Header(ArrServiceKind kind) => kind switch
    {
        ArrServiceKind.Sonarr => _sonarrHeader,
        ArrServiceKind.Radarr => _radarrHeader,
        _ => kind.ToString()
    };

    public static string RenderPinnedMessage(QueueSnapshot snapshot)
    {
        if (snapshot.IsOffline)
            return RenderOffline(snapshot.Kind, snapshot.OfflineReason ?? "unknown error");

        var sb = new StringBuilder();
        sb.AppendLine(Header(snapshot.Kind));
        sb.AppendLine();
        sb.AppendLine(RenderSummaryLine(snapshot));

        if (snapshot.Rows.Count == 0 || snapshot.TotalCount == 0)
        {
            sb.AppendLine();
            sb.Append("✅ No active downloads");
            return sb.ToString();
        }

        // Order: downloading first, then by timeleft, then everything else.
        var ordered = snapshot.Rows
            .OrderByDescending(r => r.IsDownloading)
            .ThenBy(r => r.TimeLeft ?? TimeSpan.MaxValue)
            .ThenBy(r => r.DisplayTitle)
            .ToList();

        var visible = ordered.Take(_pinnedRowCap).ToList();
        var remaining = ordered.Count - visible.Count;

        sb.AppendLine();
        sb.AppendLine("||");
        sb.AppendLine("```");
        sb.AppendLine($"{"Title",-_titleTruncatePinned} Progress  Speed     ETA      Status  Size");
        sb.AppendLine(new string('-', _titleTruncatePinned + 41));

        foreach (var row in visible)
            sb.AppendLine(FormatRow(row, _titleTruncatePinned));

        if (remaining > 0)
            sb.AppendLine($"...and {remaining} more — click 📥 for full list");

        sb.AppendLine("```");
        sb.Append("||");

        return sb.ToString();
    }

    public static string RenderOffline(ArrServiceKind kind, string reason)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header(kind));
        sb.AppendLine();
        sb.Append($"🔴 OFFLINE — {reason}");
        return sb.ToString();
    }

    /// <summary>
    /// Renders the full (non-idle) queue, split into chunks each under
    /// Discord's 2000-char message cap. Used by the DM button handler.
    /// </summary>
    public static IEnumerable<string> RenderFullListChunks(QueueSnapshot snapshot)
    {
        if (snapshot.IsOffline)
        {
            yield return RenderOffline(snapshot.Kind, snapshot.OfflineReason ?? "unknown error");
            yield break;
        }

        if (snapshot.Rows.Count == 0)
        {
            var sbIdle = new StringBuilder();
            sbIdle.AppendLine(Header(snapshot.Kind));
            sbIdle.AppendLine();
            sbIdle.Append("✅ No active downloads");
            yield return sbIdle.ToString();
            yield break;
        }

        var ordered = snapshot.Rows
            .OrderByDescending(r => r.IsDownloading)
            .ThenBy(r => r.TimeLeft ?? TimeSpan.MaxValue)
            .ThenBy(r => r.DisplayTitle)
            .ToList();

        var chunk = new StringBuilder();
        chunk.AppendLine(Header(snapshot.Kind));
        chunk.AppendLine();
        chunk.AppendLine(RenderSummaryLine(snapshot));
        chunk.AppendLine();
        chunk.AppendLine("```");
        chunk.AppendLine($"{"Title",-_titleTruncateFull} Progress  Speed     ETA      Status  Size");
        chunk.AppendLine(new string('-', _titleTruncateFull + 41));

        foreach (var row in ordered)
        {
            var line = FormatRow(row, _titleTruncateFull);

            if (chunk.Length + line.Length + 4 > _messageCharLimit)
            {
                chunk.AppendLine("```");
                yield return chunk.ToString();
                chunk = new StringBuilder();
                chunk.AppendLine(Header(snapshot.Kind) + " (continued)");
                chunk.AppendLine("```");
                chunk.AppendLine($"{"Title",-_titleTruncateFull} Progress  Speed     ETA      Status  Size");
                chunk.AppendLine(new string('-', _titleTruncateFull + 41));
            }

            chunk.AppendLine(line);
        }

        chunk.AppendLine("```");
        yield return chunk.ToString();
    }

    /// <summary>
    /// The custom_id used on the DM button for the given service.
    /// </summary>
    public static string DmButtonCustomId(ArrServiceKind kind) => kind switch
    {
        ArrServiceKind.Sonarr => "queue_dm_sonarr",
        ArrServiceKind.Radarr => "queue_dm_radarr",
        _ => $"queue_dm_{kind.ToString().ToLowerInvariant()}"
    };

    private static string RenderSummaryLine(QueueSnapshot snapshot)
    {
        var parts = new List<string>();

        if (snapshot.DownloadingCount > 0)
            parts.Add($"{snapshot.DownloadingCount} downloading");
        if (snapshot.QueuedCount > 0)
            parts.Add($"{snapshot.QueuedCount} queued");
        if (snapshot.PausedCount > 0)
            parts.Add($"{snapshot.PausedCount} paused");
        if (snapshot.WarningCount > 0)
            parts.Add($"⚠️ {snapshot.WarningCount} warning{(snapshot.WarningCount == 1 ? "" : "s")}");
        if (snapshot.ErrorCount > 0)
            parts.Add($"🔴 {snapshot.ErrorCount} error{(snapshot.ErrorCount == 1 ? "" : "s")}");

        if (parts.Count == 0)
            return "Queue empty";

        return string.Join(" · ", parts);
    }

    private static string FormatRow(QueueRow row, int titleWidth)
    {
        var title = Truncate(row.DisplayTitle, titleWidth).PadRight(titleWidth);
        var progress = $"{row.ProgressPercent,3:F0}%";
        var speed = row.SpeedBytesPerSec.HasValue ? FormatSpeed(row.SpeedBytesPerSec.Value) : "—";
        var eta = FormatEta(row.TimeLeft, row.EstimatedCompletionTime);
        var statusIcon = StatusIcon(row);
        var size = FormatBytes(row.SizeBytes);

        return $"{title} {progress,-7}  {speed,-8} {eta,-8} {statusIcon}  {size}";
    }

    private static string StatusIcon(QueueRow row)
    {
        if (row.IsError) return "🔴";
        if (row.IsWarning) return "⚠️";
        return row.Status.ToLowerInvariant() switch
        {
            "downloading" => "⬇️",
            "queued" => "⏳",
            "paused" => "⏸️",
            "delay" => "🕓",
            "completed" => "✅",
            "failed" => "❌",
            _ => "•"
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength) return value;
        return string.Concat(value.AsSpan(0, maxLength - 1), "…");
    }

    public static string FormatSpeed(long bytesPerSec)
    {
        string[] units = ["B/s", "KB/s", "MB/s", "GB/s"];
        double size = bytesPerSec;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size,3:F1}{units[unit]}";
    }

    public static string FormatBytes(decimal bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = (double)bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size,3:F1}{units[unit]}";
    }

    public static string FormatEta(TimeSpan? timeLeft, DateTime? estimatedCompletionTime)
    {
        if (timeLeft is { } tl && tl > TimeSpan.Zero)
        {
            if (tl.TotalHours >= 1)
                return $"{(int)tl.TotalHours}h{tl.Minutes:D2}m";
            if (tl.TotalMinutes >= 1)
                return $"{tl.Minutes}m{tl.Seconds:D2}s";
            return $"{tl.Seconds}s";
        }

        if (estimatedCompletionTime is { } eta)
        {
            var now = DateTime.UtcNow;
            if (eta <= now) return "now";
            var remaining = eta - now;
            if (remaining.TotalHours >= 1)
                return $"{(int)remaining.TotalHours}h{remaining.Minutes:D2}m";
            if (remaining.TotalMinutes >= 1)
                return $"{remaining.Minutes}m{remaining.Seconds:D2}s";
            return $"{remaining.Seconds}s";
        }

        return "—";
    }
}
