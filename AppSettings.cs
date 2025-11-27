namespace JellyfinReporter;

public class AppSettings
{
    public required HealthCheck HealthCheck { get; set; }
}

public class HealthCheck
{
    public required string BaseUrl { get; set; }
    public required string Endpoint { get; set; }
    public required int Interval { get; set; }
}