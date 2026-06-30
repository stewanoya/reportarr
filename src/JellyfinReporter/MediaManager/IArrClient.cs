namespace JellyfinReporter.MediaManager;

public interface IArrClient
{
    ArrServiceKind Kind { get; }
    Task<IReadOnlyList<QueueItem>> GetQueueAsync(CancellationToken cancellationToken = default);
    Task RemoveFromQueueAsync(int id, CancellationToken cancellationToken = default);
}
