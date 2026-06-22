namespace HMailServer.Core.Abstractions;

public sealed record MessageIndexingAdministrationStatus(
    int TotalMessageCount,
    int TotalIndexedCount,
    bool Enabled,
    bool IsFullTextReady,
    int QueuedMessageCount,
    string LastError);
