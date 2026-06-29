using JellyfinReporter.MediaManager;

namespace JellyfinReporter.QueueReporting;

public interface IQueueMonitor
{
    ArrServiceKind Kind { get; }
    int RefreshInterval { get; }
    Task TickAsync(CancellationToken cancellationToken = default);
}
