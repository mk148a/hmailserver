namespace HMailServer.Core.Abstractions;

public interface IPop3MailboxStore
{
    ValueTask<IReadOnlyList<Pop3MessageListing>> ListMessagesAsync(
        ImapAuthenticatedAccount account,
        CancellationToken cancellationToken);

    ValueTask<Stream> OpenMessageAsync(
        ImapAuthenticatedAccount account,
        long messageId,
        CancellationToken cancellationToken);

    ValueTask DeleteMessagesAsync(
        ImapAuthenticatedAccount account,
        IReadOnlyCollection<long> messageIds,
        CancellationToken cancellationToken);
}
