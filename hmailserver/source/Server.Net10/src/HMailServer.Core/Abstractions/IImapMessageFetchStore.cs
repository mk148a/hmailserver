namespace HMailServer.Core.Abstractions;

public interface IImapMessageFetchStore
{
    IAsyncEnumerable<ImapFetchedMessage> FetchAsync(
        ImapFetchRequest request,
        CancellationToken cancellationToken);
}
