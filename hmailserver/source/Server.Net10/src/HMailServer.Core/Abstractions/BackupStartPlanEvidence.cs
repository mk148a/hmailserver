namespace HMailServer.Core.Abstractions;

public sealed record BackupStartPlanEvidence(
    string Destination,
    int BackupOptions,
    bool BackupMessagesDbOnly,
    bool AllMessageFilesInDataDirectory,
    bool DestinationExists);
