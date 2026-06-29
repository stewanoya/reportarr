using JellyfinReporter.Configuration;
using JellyfinReporter.MediaManager;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace JellyfinReporter.Discord.Interactions;

public class QueueDmButtonModule : ComponentInteractionModule<ButtonInteractionContext>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;
    private readonly ILogger<QueueDmButtonModule> _logger;

    public QueueDmButtonModule(IHttpClientFactory httpClientFactory, AppSettings settings, ILogger<QueueDmButtonModule> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _logger = logger;
    }

    [ComponentInteraction("queue_dm_sonarr")]
    public Task<InteractionMessageProperties> SonarrAsync() => SendDmAsync(ArrServiceKind.Sonarr);

    [ComponentInteraction("queue_dm_radarr")]
    public Task<InteractionMessageProperties> RadarrAsync() => SendDmAsync(ArrServiceKind.Radarr);

    private async Task<InteractionMessageProperties> SendDmAsync(ArrServiceKind kind)
    {
        var config = GetServiceConfig(kind);
        if (config is null)
        {
            return Ephemeral($"{kind} is not enabled.");
        }

        QueueSnapshot snapshot;
        try
        {
            var client = new ArrClient(_httpClientFactory.CreateClient(), config);
            var items = await client.GetQueueAsync();
            snapshot = QueueSnapshotBuilder.Build(kind, items, new QueueStateCache(), DateTime.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DM button fetch failed for {Kind}: {Message}", kind, ex.Message);
            snapshot = QueueSnapshot.Offline(kind, "unreachable");
        }

        try
        {
            var dm = await Context.User.GetDMChannelAsync();
            foreach (var chunk in QueueTemplateHelper.RenderFullListChunks(snapshot))
                await dm.SendMessageAsync(new MessageProperties().WithContent(chunk));

            return Ephemeral($"DM'd you the full {kind} queue.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to DM {Kind} queue to user {UserId}: {Message}", kind, Context.User.Id, ex.Message);
            return Ephemeral($"Couldn't send you a DM — make sure you allow direct messages from this server's members.");
        }
    }

    private ArrServiceConfig? GetServiceConfig(ArrServiceKind kind) => kind switch
    {
        ArrServiceKind.Sonarr => _settings.Sonarr is { Enabled: true, BaseUrl: var url, ApiKey: var key } s
            ? new ArrServiceConfig(kind, url, key, s.RefreshInterval)
            : null,
        ArrServiceKind.Radarr => _settings.Radarr is { Enabled: true, BaseUrl: var url, ApiKey: var key } r
            ? new ArrServiceConfig(kind, url, key, r.RefreshInterval)
            : null,
        _ => null
    };

    private static InteractionMessageProperties Ephemeral(string content) =>
        new InteractionMessageProperties()
            .WithContent(content)
            .WithFlags(MessageFlags.Ephemeral);
}
