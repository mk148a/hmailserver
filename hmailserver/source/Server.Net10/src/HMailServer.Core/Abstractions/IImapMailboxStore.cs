namespace HMailServer.Core.Abstractions;

public interface IImapMailboxStore
{
    ValueTask<ImapMailboxSelection?> SelectMailboxAsync(
        int accountId,
        string mailboxName,
        bool readOnly,
        CancellationToken cancellationToken);
}
