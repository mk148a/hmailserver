namespace HMailServer.Core.Abstractions;

public sealed record ApplicationRuntimeSnapshot(
    int ServerState,
    string Version,
    string InitializationFile,
    string VersionArchitecture);
