namespace HMailServer.Core.Abstractions;

public sealed record ImapIdleWatchRequest(
    int AccountId,
    int FolderId,
    string MailboxName,
    long KnownExists,
    long KnownRecent);
