namespace JellyfinReporter.Reporting;

public interface IJellyfinReporterManager
{
    Task DoReportAsync(CancellationToken cancellationToken = default);
}