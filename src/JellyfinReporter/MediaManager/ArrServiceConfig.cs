namespace JellyfinReporter.MediaManager;

public sealed record ArrServiceConfig(ArrServiceKind Kind, string BaseUrl, string ApiKey, int RefreshInterval);
