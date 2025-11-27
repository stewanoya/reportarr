namespace JellyfinReporter;

public class JellyfinReporterManager(IJellyfinClient jellyfinClient, 
    AppSettings settings, 
    ILogger<JellyfinReporterManager> logger,
    IChatBot bot,
    IHostApplicationLifetime lifetime) : IJellyfinReporterManager
{
    private readonly IJellyfinClient _jellyfinClient = jellyfinClient;
    private readonly AppSettings _settings = settings;
    private readonly ILogger<JellyfinReporterManager> _logger = logger;
    private readonly IChatBot _bot = bot;
    private readonly IHostApplicationLifetime _lifetime = lifetime;

    public async Task DoReportAsync(CancellationToken cancellationToken = default)
    {
        if (_bot.CurrentStatus() == Status.Error)
        {
            _logger.LogCritical("Bot is in error status, shutting application down");
            _lifetime.StopApplication();
            return;
        }

        var isHealthy = await _jellyfinClient.CheckServerHealthAsync();

        if (_bot.CurrentStatus() == Status.NotReady)
        {
            await _bot.Init(isHealthy, cancellationToken);
        }

        await _bot.UpdateStatus(isHealthy, cancellationToken);

        await Task.Delay(_settings.HealthCheck.Interval, cancellationToken);
    }
}