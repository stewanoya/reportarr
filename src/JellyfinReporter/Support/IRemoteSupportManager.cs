namespace JellyfinReporter.Support;

public interface IRemoteSupportManager
{
    void InvokeRemoteSupport(RemoteSupportCommands command);
}