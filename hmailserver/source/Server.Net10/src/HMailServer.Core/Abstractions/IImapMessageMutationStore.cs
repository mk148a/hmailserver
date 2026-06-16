namespace HMailServer.Core.Abstractions;

public interface IImapMessageMutationStore
{
    IAsyncEnumerable<ImapStoredMessage> StoreFlagsAsync(
        ImapStoreRequest request,
        CancellationToken cancellationToken);

    IAsyncEnumerable<ImapExpungedMessage> ExpungeDeletedAsync(
        int accountId,
        int folderId,
        CancellationToken cancellationToken);
}
