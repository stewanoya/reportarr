using System.Diagnostics;

namespace JellyfinReporter;

public class RemoteSupportManager(ILogger<RemoteSupportManager> logger, AppSettings settings) : IRemoteSupportManager
{
    private readonly ILogger<RemoteSupportManager> _logger = logger;
    private readonly AppSettings _settings = settings;

    public void InvokeRemoteSupport(RemoteSupportCommands command)
    {
        if (!_settings.Support.RemoteSupportEnabled)
        {
            _logger.LogWarning("Remote support invoked, but setting is disabled. Cancelling request.");
            return;
        }

        var commandFile = Path.Combine(_settings.Support.ScriptsPath, command.ToString());

        if (!File.Exists(commandFile))
        {
            _logger.LogError("Command file for {Command} not found at path {Path}", command, commandFile);
            return;        
        }

        if (_settings.Support.HostOs == HostOs.Windows.ToString())
        {
            StartWindowsCommand(commandFile);
            return;
        }

        _logger.LogWarning("Unknown hostos configured, {HostOs}", _settings.Support.HostOs);
    }

    private static void StartWindowsCommand(string filePath)
    {
        var startInfo = new ProcessStartInfo()
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy ByPass -File \"{filePath}\"",
            UseShellExecute = false
        };

        Process.Start(startInfo);
    }

}

public enum RemoteSupportCommands
{
    Restart
}

public enum HostOs
{
    Windows
}