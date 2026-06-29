namespace JellyfinReporter.Health;

public interface IJellyfinClient
{
    Task<bool> CheckServerHealthAsync();
}