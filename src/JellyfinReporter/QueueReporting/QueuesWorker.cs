using JellyfinReporter.MediaManager;
using Microsoft.Extensions.Logging;

namespace JellyfinReporter.QueueReporting;

public sealed class QueuesWorker : BackgroundService
{
    private readonly IEnumerable<IQueueMonitor> _monitors;
    private readonly ILogger<QueuesWorker> _logger;

    public QueuesWorker(IEnumerable<IQueueMonitor> monitors, ILogger<QueuesWorker> logger)
    {
        _monitors = monitors;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var monitors = _monitors.ToList();
        if (monitors.Count == 0)
        {
            _logger.LogInformation("No queue monitors registered — QueuesWorker idle.");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Starting {Count} queue monitor(s): {Kinds}",
            monitors.Count, string.Join(", ", monitors.Select(m => m.Kind)));

        var tasks = monitors.Select(m => RunMonitorLoopAsync(m, stoppingToken)).ToArray();
        return Task.WhenAll(tasks);
    }

    private static async Task RunMonitorLoopAsync(IQueueMonitor monitor, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await monitor.TickAsync(stoppingToken);
            await Task.Delay(monitor.RefreshInterval, stoppingToken);
        }
    }
}
