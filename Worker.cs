namespace JellyfinReporter;

public class Worker(ILogger<Worker> logger, IJellyfinReporterManager reporter, AppSettings settings) : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger;
    private readonly IJellyfinReporterManager _reporter = reporter;
    private readonly AppSettings _settings = settings;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _reporter.DoReportAsync();
        }
    }
}
