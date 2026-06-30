using JellyfinReporter.Configuration;
using JellyfinReporter.Discord;
using JellyfinReporter.MediaManager;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Microsoft.Extensions.Logging;

namespace JellyfinReporter.QueueReporting;

public sealed class QueueMonitor : IQueueMonitor
{
    private readonly IArrClient _client;
    private readonly QueueStateCache _speedCache = new();
    private readonly AppSettings _settings;
    private readonly GatewayClient _gateway;
    private readonly ILogger<QueueMonitor> _logger;

    private TextChannel? _channel;
    private RestMessage? _pinnedMessage;
    private bool _initialized;

    public QueueMonitor(
        ArrServiceConfig serviceConfig,
        IArrClient client,
        AppSettings settings,
        GatewayClient gateway,
        ILogger<QueueMonitor> logger)
    {
        _client = client;
        _settings = settings;
        _gateway = gateway;
        _logger = logger;
        RefreshInterval = serviceConfig.RefreshInterval > 0 ? serviceConfig.RefreshInterval : 60_000;
    }

    public ArrServiceKind Kind => _client.Kind;
    public int RefreshInterval { get; }

    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_initialized)
                await InitChannelAsync(cancellationToken);

            QueueSnapshot snapshot;
            try
            {
                var items = await _client.GetQueueAsync(cancellationToken);
                snapshot = QueueSnapshotBuilder.Build(Kind, items, _speedCache, DateTime.UtcNow);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "{Kind} queue fetch failed: {Message}", Kind, ex.Message);
                snapshot = QueueSnapshot.Offline(Kind, ClassifyError(ex));
            }

            await RenderAsync(snapshot, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never let the monitor loop die — log and continue.
            _logger.LogError(ex, "Unexpected error in {Kind} queue monitor tick: {Message}", Kind, ex.Message);
        }
    }

    private async Task InitChannelAsync(CancellationToken cancellationToken)
    {
        var channel = await _gateway.Rest.GetChannelAsync(_settings.Discord.ChannelId, cancellationToken: cancellationToken);
        if (channel is not TextChannel textChannel)
            throw new InvalidOperationException($"Discord channel {_settings.Discord.ChannelId} is not a text channel.");

        _channel = textChannel;
        _initialized = true;
    }

    private async Task RenderAsync(QueueSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (_channel is null)
            return;

        var content = QueueTemplateHelper.RenderPinnedMessage(snapshot);
        var header = QueueTemplateHelper.Header(Kind);

        var components = snapshot.IsOffline || snapshot.Rows.Count == 0
            ? new List<IMessageComponentProperties>
            {
                new ActionRowProperties([ new ButtonProperties(
                    QueueTemplateHelper.DmButtonCustomId(Kind),
                    "📥 DM me full list",
                    ButtonStyle.Primary) ])
            }
            : QueueTemplateHelper.BuildPinnedComponents(snapshot);
        var properties = new MessageProperties
        {
            Content = content,
            Components = components
        };

        if (_pinnedMessage is null)
        {
            _pinnedMessage = await PinnedMessageLocator.FindOrCreatePinnedAsync(_channel, header, properties, cancellationToken);
        }
        else
        {
            await _pinnedMessage.ModifyAsync(p =>
            {
                p.Content = content;
                p.Components = components;
            }, cancellationToken: cancellationToken);
        }
    }

    private static string ClassifyError(Exception ex) => ex switch
    {
        HttpRequestException http => http.StatusCode is { } status && (int)status == 401
            ? "bad API key (401)"
            : http.StatusCode is { } s && (int)s == 403
                ? "forbidden (403)"
                : "unreachable",
        TaskCanceledException => "timeout",
        _ => ex.Message
    };
}
