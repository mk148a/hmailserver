namespace HMailServer.Core.Abstractions;

public sealed record ScriptingAdministrationSnapshot(
    bool Enabled,
    string Language,
    string Directory);
