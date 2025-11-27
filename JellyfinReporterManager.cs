namespace JellyfinReporter;

public class JellyfinReporterManager(IJellyfinClient jellyfinClient, AppSettings settings, ILogger<JellyfinReporterManager> logger) : IJellyfinReporterManager
{
    private readonly IJellyfinClient _jellyfinClient = jellyfinClient;
    private readonly AppSettings _settings = settings;
    private readonly ILogger<JellyfinReporterManager> _logger = logger;

    public async Task DoReportAsync()
    {
        var isHealthy = await _jellyfinClient.CheckServerHealthAsync();

        if (isHealthy)
        {
            _logger.LogInformation("Server is healthy");
        } 
        else
        {
            _logger.LogCritical("Server is unhealthy, deploying notification if enabled");    
        }

        await Task.Delay(_settings.HealthCheck.Interval);
    }
}