namespace JellyfinReporter;

public class Worker(IJellyfinReporterManager reporter) : BackgroundService
{
    private readonly IJellyfinReporterManager _reporter = reporter;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _reporter.DoReportAsync();
        }
    }
}
