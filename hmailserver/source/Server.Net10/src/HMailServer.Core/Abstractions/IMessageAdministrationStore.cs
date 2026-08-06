namespace HMailServer.Core.Abstractions;

public interface IMessageAdministrationStore
{
    ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetAccountMessagesAsync(
        int accountId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetFolderMessagesAsync(
        int folderId,
        CancellationToken cancellationToken);

    ValueTask<long> InsertMessageAsync(
        int accountId,
        int folderId,
        MessageAdministrationSnapshot snapshot,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Message insertion is not available in this store.");
}
