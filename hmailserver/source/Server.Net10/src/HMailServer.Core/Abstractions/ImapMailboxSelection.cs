namespace HMailServer.Core.Abstractions;

public sealed record ImapMailboxSelection(
    int AccountId,
    int FolderId,
    string Name,
    long Exists,
    long Recent,
    long UidValidity,
    long UidNext,
    long? FirstUnseenUid,
    bool IsReadOnly,
    bool RequestedReadOnly = false);
