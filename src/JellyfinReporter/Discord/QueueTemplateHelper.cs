using System.Text;
using JellyfinReporter.MediaManager;
using NetCord;
using NetCord.Rest;

namespace JellyfinReporter.Discord;

/// <summary>
/// A rendered chunk of the full-list DM: text content plus optional
/// interactive components (remove buttons) for the items in this chunk.
/// </summary>
public sealed record QueueChunkMessage(string Content, List<IMessageComponentProperties> Components);

public static class QueueTemplateHelper
{
    private const string _sonarrHeader = "📺 TV Shows Queue";
    private const string _radarrHeader = "🎬 Movies Queue";

    // Pinned message shows the top 5 soonest-to-finish; the DM button reveals the rest.
    private const int _pinnedRowCap = 5;
    private const int _titleTruncateFull = 60;
    private const int _messageCharLimit = 1900; // safety margin under Discord's 2000-cap

    // Discord select menus allow up to 25 options per menu.
    public const int SelectPageSize = 25;

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

        // Order: downloading first, then by timeleft (soonest first).
        var ordered = snapshot.Rows
            .OrderByDescending(r => r.IsDownloading)
            .ThenBy(r => r.TimeLeft ?? TimeSpan.MaxValue)
            .ThenBy(r => r.DisplayTitle)
            .ToList();

        var visible = ordered.Take(_pinnedRowCap).ToList();
        var remaining = ordered.Count - visible.Count;

        sb.AppendLine();

        foreach (var row in visible)
        {
            sb.AppendLine(FormatRow(row));
            sb.AppendLine();
        }

        if (remaining > 0)
            sb.AppendLine($"…and {remaining} more — click 📥 for full list");

        // Trailing blank lines so the button row isn't cramped against the list.
        sb.AppendLine();
        sb.AppendLine();
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
    /// Renders the full queue, split into chunks each under
    /// Discord's 2000-char message cap. The last content chunk
    /// carries the delete + DM buttons. Used by the DM button handler.
    /// </summary>
    public static IEnumerable<QueueChunkMessage> RenderFullListMessages(QueueSnapshot snapshot)
    {
        if (snapshot.IsOffline)
        {
            yield return new QueueChunkMessage(
                RenderOffline(snapshot.Kind, snapshot.OfflineReason ?? "unknown error"),
                []);
            yield break;
        }

        if (snapshot.Rows.Count == 0)
        {
            var sbIdle = new StringBuilder();
            sbIdle.AppendLine(Header(snapshot.Kind));
            sbIdle.AppendLine();
            sbIdle.Append("✅ No active downloads");
            yield return new QueueChunkMessage(sbIdle.ToString(), []);
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

        foreach (var row in ordered)
        {
            var line = FormatRow(row);

            if (chunk.Length + line.Length + 3 > _messageCharLimit)
            {
                yield return new QueueChunkMessage(chunk.ToString().TrimEnd(), []);
                chunk = new StringBuilder();
                chunk.AppendLine(Header(snapshot.Kind) + " (continued)");
                chunk.AppendLine();
            }

            chunk.AppendLine(line);
            chunk.AppendLine();
        }

        // Last content chunk gets the delete button.
        var components = new List<IMessageComponentProperties>
        {
            new ActionRowProperties([
                new ButtonProperties(
                    DeleteButtonCustomId(snapshot.Kind),
                    "🗑️ Delete & search again",
                    ButtonStyle.Danger)
            ])
        };
        yield return new QueueChunkMessage(chunk.ToString().TrimEnd(), components);

        // Legend always goes as its own message so it can never blow past the
        // 2000-char cap by being appended to an already-full chunk.
        yield return new QueueChunkMessage(RenderLegend(), []);
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

    // --- Delete flow custom_id scheme ---
    // Delete button (on channel/DM):  queue_del_{kind}
    // Page nav button (ephemeral):    queue_page_{kind}:{page}
    // Select menu (ephemeral):         queue_sel_{kind}
    // Review button (ephemeral):       queue_rev_{kind}
    // Confirm delete (ephemeral):      queue_delc_{kind}
    // Cancel (ephemeral):             queue_delx

    public static string DeleteButtonCustomId(ArrServiceKind kind) =>
        $"queue_del_{kind.ToString().ToLowerInvariant()}";

    public static string PageButtonCustomId(ArrServiceKind kind, int page) =>
        $"queue_page_{kind.ToString().ToLowerInvariant()}:{page}";

    public static string SelectMenuCustomId(ArrServiceKind kind) =>
        $"queue_sel_{kind.ToString().ToLowerInvariant()}";

    public static string ReviewButtonCustomId(ArrServiceKind kind) =>
        $"queue_rev_{kind.ToString().ToLowerInvariant()}";

    public static string ConfirmDeleteCustomId(ArrServiceKind kind) =>
        $"queue_delc_{kind.ToString().ToLowerInvariant()}";

    public const string CancelDeleteCustomId = "queue_delx";

    /// <summary>
    /// Parses a custom_id with format {prefix}_{kind} and returns the kind.
    /// </summary>
    public static bool TryParseKind(string customId, out ArrServiceKind kind)
    {
        kind = default;
        var underscore = customId.LastIndexOf('_');
        if (underscore < 0 || underscore >= customId.Length - 1) return false;
        var kindStr = customId[(underscore + 1)..];
        return Enum.TryParse<ArrServiceKind>(kindStr, ignoreCase: true, out kind);
    }

    /// <summary>
    /// Builds the action-row components for the pinned (channel) message:
    /// one row with a delete button and the DM button.
    /// </summary>
    public static List<IMessageComponentProperties> BuildPinnedComponents(QueueSnapshot snapshot)
    {
        return
        [
            new ActionRowProperties([
                new ButtonProperties(
                    DeleteButtonCustomId(snapshot.Kind),
                    "🗑️ Delete & search again",
                    ButtonStyle.Danger),
                new ButtonProperties(
                    DmButtonCustomId(snapshot.Kind),
                    "📥 DM me full list",
                    ButtonStyle.Primary)
            ])
        ];
    }

    /// <summary>
    /// Builds the content text for the paginated select ephemeral message.
    /// </summary>
    public static string RenderSelectContent(ArrServiceKind kind, int totalItems, int selectedCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🗑️ **{Header(kind)}** — Select items to remove, blocklist, and search for replacements.");
        if (totalItems > SelectPageSize)
            sb.AppendLine($"📋 {totalItems} items in queue — use ◀️ ▶️ to browse all pages.");
        if (selectedCount > 0)
            sb.AppendLine($"✅ **{selectedCount}** item(s) selected so far.");
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Builds the components for one page of the paginated select menu:
    /// Row 1 = StringMenu with up to 25 items, Row 2 = nav + review buttons.
    /// </summary>
    public static List<IMessageComponentProperties> BuildPagedSelectComponents(
        ArrServiceKind kind, IReadOnlyList<QueueRow> orderedRows, int page, HashSet<int> selectedIds)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(orderedRows.Count / (double)SelectPageSize));
        var pageRows = orderedRows
            .Skip((page - 1) * SelectPageSize)
            .Take(SelectPageSize)
            .ToList();

        var components = new List<IMessageComponentProperties>();

        // Row 1: Select menu
        if (pageRows.Count > 0)
        {
            var options = pageRows
                .Select(r => new StringMenuSelectOptionProperties(
                    Truncate(r.DisplayTitle, 100),
                    r.Id.ToString())
                {
                    Description = Truncate(
                        $"{r.ProgressPercent:F0}% · {(r.SpeedBytesPerSec.HasValue ? FormatSpeed(r.SpeedBytesPerSec.Value) : "—")} · ETA {FormatEta(r.TimeLeft, r.EstimatedCompletionTime)}",
                        100),
                    Default = selectedIds.Contains(r.Id)
                })
                .ToList();

            components.Add(new StringMenuProperties(SelectMenuCustomId(kind), options)
            {
                Placeholder = "Select items to remove and blocklist…",
                MinValues = 0,
                MaxValues = options.Count
            });
        }

        // Row 2: Nav buttons + review
        var navButtons = new List<IActionRowComponentProperties>();

        navButtons.Add(page > 1
            ? new ButtonProperties(PageButtonCustomId(kind, page - 1), "◀️", ButtonStyle.Secondary)
            : new ButtonProperties("queue_nav_disabled_prev", "◀️", ButtonStyle.Secondary) { Disabled = true });

        navButtons.Add(new ButtonProperties(
            "queue_nav_disabled_info",
            $"Page {page}/{totalPages}",
            ButtonStyle.Secondary) { Disabled = true });

        navButtons.Add(page < totalPages
            ? new ButtonProperties(PageButtonCustomId(kind, page + 1), "▶️", ButtonStyle.Secondary)
            : new ButtonProperties("queue_nav_disabled_next", "▶️", ButtonStyle.Secondary) { Disabled = true });

        navButtons.Add(new ButtonProperties(
            ReviewButtonCustomId(kind),
            selectedIds.Count > 0 ? $"✅ Review ({selectedIds.Count})" : "✅ Review",
            ButtonStyle.Primary));

        components.Add(new ActionRowProperties(navButtons));

        return components;
    }

    /// <summary>
    /// Builds the confirmation components: Confirm + Cancel + Back buttons.
    /// </summary>
    public static List<IMessageComponentProperties> BuildConfirmComponents(ArrServiceKind kind)
    {
        return
        [
            new ActionRowProperties([
                new ButtonProperties(
                    ConfirmDeleteCustomId(kind),
                    "✅ Confirm Remove",
                    ButtonStyle.Danger),
                new ButtonProperties(
                    PageButtonCustomId(kind, 1),
                    "◀️ Back",
                    ButtonStyle.Secondary),
                new ButtonProperties(
                    CancelDeleteCustomId,
                    "❌ Cancel",
                    ButtonStyle.Secondary)
            ])
        ];
    }

    /// <summary>
    /// Renders the confirmation content listing all selected items.
    /// </summary>
    public static string RenderConfirmContent(ArrServiceKind kind, List<(int Id, string Title)> selectedItems)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🗑️ Remove **{selectedItems.Count}** item(s) from **{Header(kind)}**?");
        sb.AppendLine("This will remove them from the download client, blocklist the releases, and search for replacements.");
        sb.AppendLine();
        foreach (var (_, title) in selectedItems)
            sb.AppendLine($"• {Truncate(title, 60)}");
        return sb.ToString().TrimEnd();
    }

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

    private static string RenderLegend()
    {
        return """
            📖 Legend
            ⬇️ Downloading · ⏳ Queued · ⏸️ Paused/Stalled · 🕓 Delay
            ✅ Completed · ❌ Failed · 🔴 Error · ⚠️ Warning
            🔌 Download client unavailable · 🔁 Fallback · ❓ Unknown
            ℹ️ = status message follows the item
            """;
    }

    private static string FormatRow(QueueRow row)
    {
        var icon = StatusIcon(row);
        var progress = $"{row.ProgressPercent:F0}%";
        var speed = row.SpeedBytesPerSec.HasValue ? FormatSpeed(row.SpeedBytesPerSec.Value) : "—";
        var eta = FormatEta(row.TimeLeft, row.EstimatedCompletionTime);

        var line = $"{icon} **{Truncate(row.DisplayTitle, _titleTruncateFull)}** — {progress} · {speed} · ETA {eta}";

        if (!string.IsNullOrWhiteSpace(row.StatusMessageText))
        {
            var msgIcon = row.IsError ? "🔴" : row.IsWarning ? "⚠️" : "ℹ️";
            line += $"\n   {msgIcon} {Truncate(row.StatusMessageText, _titleTruncateFull)}";
        }

        return line;
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
            "warning" => "⚠️",
            "downloadclientunavailable" => "🔌",
            "fallback" => "🔁",
            "unknown" => "❓",
            _ => "⏸️" // stalled / unknown idle state
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
        return $"{size:F1}{units[unit]}";
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
        return $"{size:F1}{units[unit]}";
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
