namespace HMailServer.Core.Abstractions;

public interface IImapMailboxStore
{
    ValueTask<ImapMailboxSelection?> SelectMailboxAsync(
        int accountId,
        string mailboxName,
        bool readOnly,
        CancellationToken cancellationToken);
}

public interface IImapSelectedMailboxAuthorization
{
    ValueTask<ImapMailboxSelection?> RevalidateSelectedMailboxAsync(
        int requesterAccountId,
        ImapMailboxSelection selectedMailbox,
        CancellationToken cancellationToken);
}
