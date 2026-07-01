namespace HMailServer.Core.Abstractions;

public sealed record BackupSettingsAdministrationSnapshot(
    string Destination,
    int Options,
    string LogDirectory);
