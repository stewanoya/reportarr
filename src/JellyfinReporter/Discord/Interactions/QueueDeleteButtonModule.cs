using System.Collections.Concurrent;
using System.Text;
using JellyfinReporter.Configuration;
using JellyfinReporter.MediaManager;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace JellyfinReporter.Discord.Interactions;

/// <summary>
/// Tracks queue item selections per page across the paginated delete flow,
/// keyed by (userId, kind).
/// </summary>
public sealed class QueueSelectionCache
{
    private readonly ConcurrentDictionary<(ulong UserId, ArrServiceKind Kind), Dictionary<int, HashSet<int>>> _cache = new();

    public void StorePage(ulong userId, ArrServiceKind kind, int page, IEnumerable<int> ids)
    {
        var key = (userId, kind);
        _cache.AddOrUpdate(
            key,
            _ => new Dictionary<int, HashSet<int>> { [page] = new(ids) },
            (_, existing) => { existing[page] = new(ids); return existing; });
    }

    public HashSet<int> RetrieveAll(ulong userId, ArrServiceKind kind)
    {
        if (!_cache.TryGetValue((userId, kind), out var pages))
            return [];
        var all = new HashSet<int>();
        foreach (var ids in pages.Values)
            all.UnionWith(ids);
        return all;
    }

    public HashSet<int> GetPageSelections(ulong userId, ArrServiceKind kind, int page)
    {
        if (!_cache.TryGetValue((userId, kind), out var pages))
            return [];
        return pages.TryGetValue(page, out var ids) ? new HashSet<int>(ids) : [];
    }

    public void Clear(ulong userId, ArrServiceKind kind) =>
        _cache.TryRemove((userId, kind), out _);
}

/// <summary>
/// Handles the "Delete &amp; search again" button on queue messages.
/// Flow: button click → ephemeral paginated select menu → user picks items
/// across pages → review confirmation → confirm → API delete.
/// </summary>
public class QueueDeleteButtonModule : ComponentInteractionModule<ButtonInteractionContext>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;
    private readonly QueueSelectionCache _selectionCache;
    private readonly ILogger<QueueDeleteButtonModule> _logger;

    public QueueDeleteButtonModule(
        IHttpClientFactory httpClientFactory,
        AppSettings settings,
        QueueSelectionCache selectionCache,
        ILogger<QueueDeleteButtonModule> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _selectionCache = selectionCache;
        _logger = logger;
    }

    // --- Delete button (on channel pinned message and DM full list) ---

    [ComponentInteraction("queue_del_sonarr")]
    public Task<InteractionMessageProperties> DeleteSonarrAsync() =>
        ShowSelectMenuPageAsync(ArrServiceKind.Sonarr, page: 1, newMessage: true);

    [ComponentInteraction("queue_del_radarr")]
    public Task<InteractionMessageProperties> DeleteRadarrAsync() =>
        ShowSelectMenuPageAsync(ArrServiceKind.Radarr, page: 1, newMessage: true);

    // --- Page nav buttons (on ephemeral) ---

    [ComponentInteraction("queue_page_sonarr")]
    public Task<InteractionMessageProperties> PageSonarrAsync(int page) =>
        ShowSelectMenuPageAsync(ArrServiceKind.Sonarr, page, newMessage: false);

    [ComponentInteraction("queue_page_radarr")]
    public Task<InteractionMessageProperties> PageRadarrAsync(int page) =>
        ShowSelectMenuPageAsync(ArrServiceKind.Radarr, page, newMessage: false);

    // --- Review button (on ephemeral) ---

    [ComponentInteraction("queue_rev_sonarr")]
    public Task<InteractionMessageProperties> ReviewSonarrAsync() =>
        ShowReviewAsync(ArrServiceKind.Sonarr);

    [ComponentInteraction("queue_rev_radarr")]
    public Task<InteractionMessageProperties> ReviewRadarrAsync() =>
        ShowReviewAsync(ArrServiceKind.Radarr);

    // --- Confirm button (on ephemeral confirmation) ---

    [ComponentInteraction("queue_delc_sonarr")]
    public Task<InteractionMessageProperties> ConfirmSonarrAsync() =>
        ConfirmDeleteAsync(ArrServiceKind.Sonarr);

    [ComponentInteraction("queue_delc_radarr")]
    public Task<InteractionMessageProperties> ConfirmRadarrAsync() =>
        ConfirmDeleteAsync(ArrServiceKind.Radarr);

    // --- Cancel button ---

    [ComponentInteraction("queue_delx")]
    public Task<InteractionMessageProperties> CancelAsync()
    {
        // Cancel doesn't carry kind in its custom_id, so we can't clear a specific cache entry.
        // The cache entries are keyed by (userId, kind) and will be cleaned up on next use or expiry.
        return Task.FromResult(Ephemeral("❌ Cancelled."));
    }

    // --- Implementation ---

    private async Task<InteractionMessageProperties> ShowSelectMenuPageAsync(
        ArrServiceKind kind, int page, bool newMessage)
    {
        var config = GetServiceConfig(kind);
        if (config is null)
            return Ephemeral($"{kind} is not enabled.");

        QueueSnapshot snapshot;
        try
        {
            var client = new ArrClient(_httpClientFactory.CreateClient(), config);
            var items = await client.GetQueueAsync();
            snapshot = QueueSnapshotBuilder.Build(kind, items, new QueueStateCache(), DateTime.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Delete flow fetch failed for {Kind}: {Message}", kind, ex.Message);
            return Ephemeral($"⚠️ Could not reach {kind} to list queue items.");
        }

        if (snapshot.Rows.Count == 0)
            return Ephemeral("✅ No active downloads to remove.");

        var ordered = OrderRows(snapshot.Rows);
        var totalPages = Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)QueueTemplateHelper.SelectPageSize));
        page = Math.Clamp(page, 1, totalPages);

        var selectedIds = _selectionCache.RetrieveAll(Context.User.Id, kind);

        var content = QueueTemplateHelper.RenderSelectContent(kind, ordered.Count, selectedIds.Count);
        var components = QueueTemplateHelper.BuildPagedSelectComponents(kind, ordered, page, selectedIds);

        return new InteractionMessageProperties()
            .WithContent(content)
            .WithComponents(components)
            .WithFlags(MessageFlags.Ephemeral);
    }

    private async Task<InteractionMessageProperties> ShowReviewAsync(ArrServiceKind kind)
    {
        var config = GetServiceConfig(kind);
        if (config is null)
            return Ephemeral($"{kind} is not enabled.");

        var selectedIds = _selectionCache.RetrieveAll(Context.User.Id, kind).ToList();
        if (selectedIds.Count == 0)
            return Ephemeral("⚠️ No items selected. Please use the dropdown to select items first.");

        // Look up titles for the confirmation.
        List<(int Id, string Title)> items;
        try
        {
            var client = new ArrClient(_httpClientFactory.CreateClient(), config);
            var queueItems = await client.GetQueueAsync();
            var snapshot = QueueSnapshotBuilder.Build(kind, queueItems, new QueueStateCache(), DateTime.UtcNow);
            var titleMap = snapshot.Rows.ToDictionary(r => r.Id, r => r.DisplayTitle);
            items = selectedIds
                .Select(id => (id, titleMap.TryGetValue(id, out var t) ? t : $"item #{id}"))
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Review lookup failed for {Kind}: {Message}", kind, ex.Message);
            items = selectedIds.Select(id => (id, $"item #{id}")).ToList();
        }

        var content = QueueTemplateHelper.RenderConfirmContent(kind, items);
        var components = QueueTemplateHelper.BuildConfirmComponents(kind);

        return new InteractionMessageProperties()
            .WithContent(content)
            .WithComponents(components)
            .WithFlags(MessageFlags.Ephemeral);
    }

    private async Task<InteractionMessageProperties> ConfirmDeleteAsync(ArrServiceKind kind)
    {
        var config = GetServiceConfig(kind);
        if (config is null)
            return Ephemeral($"{kind} is not enabled.");

        var selectedIds = _selectionCache.RetrieveAll(Context.User.Id, kind).ToList();
        if (selectedIds.Count == 0)
            return Ephemeral("⚠️ No items selected. Please use the dropdown to select items first.");

        // Look up titles for the result message.
        Dictionary<int, string> titles;
        try
        {
            var client = new ArrClient(_httpClientFactory.CreateClient(), config);
            var items = await client.GetQueueAsync();
            var snapshot = QueueSnapshotBuilder.Build(kind, items, new QueueStateCache(), DateTime.UtcNow);
            titles = snapshot.Rows.ToDictionary(r => r.Id, r => r.DisplayTitle);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Confirm lookup failed for {Kind}: {Message}", kind, ex.Message);
            titles = [];
        }

        var succeeded = new List<string>();
        var failed = new List<string>();

        try
        {
            var client = new ArrClient(_httpClientFactory.CreateClient(), config);
            foreach (var id in selectedIds)
            {
                try
                {
                    await client.RemoveFromQueueAsync(id);
                    var title = titles.TryGetValue(id, out var t) ? t : $"item #{id}";
                    succeeded.Add(TruncateTitle(title));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Failed to remove {Kind} queue item {Id}", kind, id);
                    failed.Add($"item #{id}");
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected error during bulk delete for {Kind}", kind);
            return Ephemeral($"⚠️ An error occurred while removing items from {kind}.");
        }

        _selectionCache.Clear(Context.User.Id, kind);

        var sb = new StringBuilder();
        if (succeeded.Count > 0)
        {
            sb.AppendLine($"✅ Removed {succeeded.Count} item(s) from {kind} — replacements will be searched for:");
            foreach (var title in succeeded)
                sb.AppendLine($"  • {title}");
        }
        if (failed.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"⚠️ Could not remove {failed.Count} item(s):");
            foreach (var f in failed)
                sb.AppendLine($"  • {f}");
        }
        if (succeeded.Count == 0 && failed.Count > 0)
            sb.AppendLine($"❌ No items were removed from {kind}.");

        return Ephemeral(sb.ToString().TrimEnd());
    }

    // --- Helpers ---

    internal static List<QueueRow> OrderRows(IReadOnlyList<QueueRow> rows) =>
        rows
            .OrderByDescending(r => r.IsDownloading)
            .ThenBy(r => r.TimeLeft ?? TimeSpan.MaxValue)
            .ThenBy(r => r.DisplayTitle)
            .ToList();

    internal ArrServiceConfig? GetServiceConfig(ArrServiceKind kind) => kind switch
    {
        ArrServiceKind.Sonarr => _settings.Sonarr is { Enabled: true, BaseUrl: var url, ApiKey: var key } s
            ? new ArrServiceConfig(kind, url, key, s.RefreshInterval)
            : null,
        ArrServiceKind.Radarr => _settings.Radarr is { Enabled: true, BaseUrl: var url, ApiKey: var key } r
            ? new ArrServiceConfig(kind, url, key, r.RefreshInterval)
            : null,
        _ => null
    };

    private static string TruncateTitle(string title) =>
        title.Length <= 60 ? title : string.Concat(title.AsSpan(0, 59), "…");

    private static InteractionMessageProperties Ephemeral(string content) =>
        new InteractionMessageProperties()
            .WithContent(content)
            .WithFlags(MessageFlags.Ephemeral);
}

/// <summary>
/// Handles the StringMenu (dropdown) selection from the delete flow.
/// Stores per-page selections and modifies the message to update the count.
/// </summary>
public class QueueSelectModule : ComponentInteractionModule<StringMenuInteractionContext>
{
    private readonly QueueSelectionCache _selectionCache;

    public QueueSelectModule(QueueSelectionCache selectionCache)
    {
        _selectionCache = selectionCache;
    }

    [ComponentInteraction("queue_sel_sonarr")]
    public Task<InteractionMessageProperties> SelectSonarrAsync() =>
        StoreSelectionAsync(ArrServiceKind.Sonarr);

    [ComponentInteraction("queue_sel_radarr")]
    public Task<InteractionMessageProperties> SelectRadarrAsync() =>
        StoreSelectionAsync(ArrServiceKind.Radarr);

    private Task<InteractionMessageProperties> StoreSelectionAsync(ArrServiceKind kind)
    {
        var selectedValues = Context.Interaction.Data.SelectedValues;
        var selectedIds = selectedValues
            .Where(v => int.TryParse(v, out _))
            .Select(int.Parse)
            .ToList();

        // We don't know which page we're on from the interaction alone,
        // but the selected IDs tell us which items were on this page.
        // Store them under page 0 as a merge — the cache merges all pages anyway.
        _selectionCache.StorePage(Context.User.Id, kind, 0, selectedIds);

        var allSelected = _selectionCache.RetrieveAll(Context.User.Id, kind);

        var content = QueueTemplateHelper.RenderSelectContent(kind, 0, allSelected.Count);
        // Note: we can't rebuild the dropdown here because we don't have the full queue.
        // The message keeps its existing components; we just update the text.
        return Task.FromResult(new InteractionMessageProperties()
            .WithContent(content)
            .WithFlags(MessageFlags.Ephemeral));
    }
}
