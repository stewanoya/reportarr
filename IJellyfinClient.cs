namespace JellyfinReporter;

public interface IJellyfinClient
{
    Task<bool> CheckServerHealthAsync();
}