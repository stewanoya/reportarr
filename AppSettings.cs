namespace JellyfinReporter;

public class AppSettings
{
    public required HealthCheck HealthCheck { get; set; }
    public required Discord Discord { get; set; }
}

public class HealthCheck
{
    public required string BaseUrl { get; set; }
    public required string Endpoint { get; set; }
    public required int Interval { get; set; }
}

public class Discord
{
    public required string Token { get; set; }
    public required string ApplicationId { get; set; }
    public required string PublicKey { get; set; }
    public required ulong ChannelId { get; set; }
    public required ulong AdminUserId { get; set; }
}