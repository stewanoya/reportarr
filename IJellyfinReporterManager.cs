namespace JellyfinReporter;

public interface IJellyfinReporterManager
{
    Task DoReportAsync(CancellationToken cancellationToken = default);
}