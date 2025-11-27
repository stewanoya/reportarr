namespace JellyfinReporter;

public class AppSettings
{
    public required HealthCheck HealthCheck { get; set; }
    public required Discord Discord { get; set; }
    public required Support Support { get; set; }
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

public class Support
{
    public bool RemoteSupportEnabled { get; set; } = false;
    public string HostOs { get; set; } = "Windows";
    public RemoteSupportCommands[] AllowedCommands { get; set; } = [];
    public required string ScriptsPath { get; set; }
}