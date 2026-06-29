using JellyfinReporter.Support;

namespace JellyfinReporter.Configuration;

public class AppSettings
{
    public required HealthCheck HealthCheck { get; set; }
    public required Discord Discord { get; set; }
    public Support? Support { get; set; }
    public SonarrConfig? Sonarr { get; set; }
    public RadarrConfig? Radarr { get; set; }
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

public class SonarrConfig
{
    public bool Enabled { get; set; } = false;
    public required string BaseUrl { get; set; }
    public required string ApiKey { get; set; }
    public int RefreshInterval { get; set; } = 60_000;
}

public class RadarrConfig
{
    public bool Enabled { get; set; } = false;
    public required string BaseUrl { get; set; }
    public required string ApiKey { get; set; }
    public int RefreshInterval { get; set; } = 60_000;
}