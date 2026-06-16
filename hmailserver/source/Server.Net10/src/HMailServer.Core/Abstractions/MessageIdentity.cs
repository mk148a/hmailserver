namespace HMailServer.Core.Abstractions;

public readonly record struct MessageIdentity(
    long MessageId,
    int AccountId,
    int FolderId,
    long Uid);
