using JellyfinReporter.Configuration;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace JellyfinReporter.Discord;

public class ChatBot(AppSettings appSettings, GatewayClient client, ILogger<ChatBot> logger) : IChatBot
{
    private readonly AppSettings _appSettings = appSettings;
    private readonly GatewayClient _client = client;
    private readonly ILogger<ChatBot> _logger = logger;

    private TextChannel? _tChannel;
    private RestMessage? _serverStatusMessage;
    private List<ulong> _serverDownMessageIds = [];

    private Status _status { get; set; } = Status.NotReady;

    public Status CurrentStatus() => _status;

    public async Task Init(bool isHealthy, CancellationToken cancellationToken = default)
    {
        try
        {
            var channel = await _client.Rest.GetChannelAsync(_appSettings.Discord.ChannelId, cancellationToken: cancellationToken);

            if (channel == null || channel is not TextChannel)
            {
                _status = Status.Error;
                return;
            }

            _tChannel = (TextChannel)channel;

            var content = MessageTemplateHelper.GetServerStatusMessage(isHealthy);
            var messageProperties = new MessageProperties() { Content = content };

            // Look for an existing pinned message with the health header, or
            // adopt an orphan pinned message (legacy, starting with "```") and
            // reformat it to the new header scheme. Otherwise create + pin new.
            _serverStatusMessage = await PinnedMessageLocator.FindOrAdoptOrphanAsync(
                _tChannel, MessageTemplateHelper.HealthHeader, messageProperties, cancellationToken);

            if (_serverStatusMessage == null)
            {
                _serverStatusMessage = await _tChannel.SendMessageAsync(messageProperties, cancellationToken: cancellationToken);
                await _serverStatusMessage.PinAsync(cancellationToken: cancellationToken);
            }

            _status = Status.Ready;
       }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Error during chatbot init - {Message}", ex.Message);
            _status = Status.Error;
        }
    }

    public async Task UpdateStatus(bool isHealthy, CancellationToken cancellationToken = default)
    {
        await _serverStatusMessage!
            .ModifyAsync(i => i.Content = MessageTemplateHelper.GetServerStatusMessage(isHealthy), 
                cancellationToken: cancellationToken);

        if (!isHealthy && _serverDownMessageIds.Count < 2)
        {
            var messageProperties = new MessageProperties() 
            { 
                Content = MessageTemplateHelper.GetUnhealthyTaggedMessage(_appSettings.Discord.AdminUserId) 
            };

            var message = await _tChannel!.SendMessageAsync(messageProperties, cancellationToken: cancellationToken);

            _serverDownMessageIds.Add(message.Id);
        }

        if (isHealthy && _serverDownMessageIds.Count > 0)
        {
            await _tChannel!.DeleteMessagesAsync(_serverDownMessageIds, cancellationToken: cancellationToken);
            _serverDownMessageIds.Clear();
        }
    }
}

public enum Status
{
    NotReady,
    Ready,
    Error,
}