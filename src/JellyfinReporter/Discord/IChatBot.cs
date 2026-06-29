namespace JellyfinReporter.Discord;

public interface IChatBot
{
    Task Init(bool isHealthy, CancellationToken cancellationToken = default);
    Task UpdateStatus(bool isHealthy, CancellationToken cancellationToken = default);
    Status CurrentStatus();
}