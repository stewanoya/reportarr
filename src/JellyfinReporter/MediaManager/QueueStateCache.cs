using System.Collections.Concurrent;

namespace JellyfinReporter.MediaManager;

/// <summary>
/// Tracks the last-known SizeLeft per download between consecutive ticks,
/// so download speed can be derived as (prevSizeLeft - curSizeLeft) / elapsed.
/// </summary>
public sealed class QueueStateCache
{
    private readonly ConcurrentDictionary<string, (decimal SizeLeft, DateTime At)> _state = new();

    /// <summary>
    /// Returns the derived speed in bytes/sec for a download, or null if it
    /// can't be computed (first tick, &lt;1s elapsed, size increased, or not
    /// currently downloading).
    /// </summary>
    public long? UpdateAndGetSpeed(string key, decimal currentSizeLeft, DateTime now, bool isDownloading)
    {
        if (!isDownloading)
        {
            _state.TryRemove(key, out _);
            return null;
        }

        if (!_state.TryGetValue(key, out var prev))
        {
            _state[key] = (currentSizeLeft, now);
            return null;
        }

        var elapsed = (now - prev.At).TotalSeconds;
        var delta = prev.SizeLeft - currentSizeLeft;

        // Reset if size grew (re-download / different item reusing the id)
        // or if less than a second has passed (avoid div-by-zero / spikes).
        if (delta < 0)
        {
            _state[key] = (currentSizeLeft, now);
            return null;
        }

        if (elapsed < 1.0)
            return null;

        _state[key] = (currentSizeLeft, now);
        return (long)(delta / (decimal)elapsed);
    }

    /// <summary>
    /// Drops keys no longer present in the queue so the cache doesn't grow
    /// unbounded across long-running sessions.
    /// </summary>
    public void PruneTo(IEnumerable<string> liveKeys)
    {
        var live = new HashSet<string>(liveKeys);
        foreach (var kvp in _state)
            if (!live.Contains(kvp.Key))
                _state.TryRemove(kvp.Key, out _);
    }
}
