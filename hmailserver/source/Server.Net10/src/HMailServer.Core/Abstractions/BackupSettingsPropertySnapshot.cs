namespace HMailServer.Core.Abstractions;

public sealed record BackupSettingsPropertySnapshot(
    string Name,
    long LongValue,
    string StringValue);
