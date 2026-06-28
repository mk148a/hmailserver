namespace HMailServer.Core.Abstractions;

public sealed record DatabaseAdministrationSnapshot(
    int RequiredVersion,
    int? CurrentVersion,
    int DatabaseType,
    bool DatabaseExists,
    bool IsConnected,
    string ServerName,
    string DatabaseName);
