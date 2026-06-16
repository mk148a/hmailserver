namespace HMailServer.Core.Abstractions;

public interface IImapMailboxDiscoveryStore
{
    IAsyncEnumerable<ImapMailboxListEntry> ListMailboxesAsync(
        int accountId,
        string referenceName,
        string mailboxPattern,
        bool subscribedOnly,
        CancellationToken cancellationToken);

    ValueTask<ImapMailboxStatus?> GetStatusAsync(
        int accountId,
        string mailboxName,
        IReadOnlyList<ImapStatusItem> items,
        CancellationToken cancellationToken);
}
