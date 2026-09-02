namespace HMailServer.Core.Abstractions;

public sealed record ImapStoreRequest(
    int AccountId,
    int FolderId,
    IReadOnlyList<ImapIdRange> MessageSet,
    bool UseUid,
    ImapStoreMode Mode,
    byte Flags,
    bool Silent,
    int? RequesterAccountId = null);
