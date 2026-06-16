namespace HMailServer.Core.Abstractions;

public sealed record ImapCopyRequest(
    int SourceAccountId,
    int SourceFolderId,
    int DestinationAccountId,
    int DestinationFolderId,
    IReadOnlyList<ImapIdRange> MessageSet,
    bool UseUid,
    bool DeleteSource);
