namespace HMailServer.Core.Abstractions;

public sealed class ServerApplicationRuntimeStore : IApplicationRuntimeStore
{
    private readonly ServerStatusRuntimeState _statusRuntimeState;
    private readonly string _version;
    private readonly string _initializationFile;

    public ServerApplicationRuntimeStore(
        ServerStatusRuntimeState statusRuntimeState,
        string version,
        string initializationFile)
    {
        ArgumentNullException.ThrowIfNull(statusRuntimeState);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(initializationFile);

        _statusRuntimeState = statusRuntimeState;
        _version = version;
        _initializationFile = Path.GetFullPath(initializationFile);
    }

    public ApplicationRuntimeSnapshot GetSnapshot()
    {
        var status = _statusRuntimeState.Capture();
        return new ApplicationRuntimeSnapshot(
            status.ServerState,
            _version,
            _initializationFile,
            Environment.Is64BitProcess ? "x64" : "x86");
    }
}
