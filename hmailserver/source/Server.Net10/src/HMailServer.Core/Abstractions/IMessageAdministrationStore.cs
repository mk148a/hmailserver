namespace HMailServer.Core.Abstractions;

public interface IMessageAdministrationStore
{
    ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetAccountMessagesAsync(
        int accountId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetFolderMessagesAsync(
        int accountId,
        int folderId,
        CancellationToken cancellationToken);

    ValueTask<MessageAdministrationInsertResult> InsertMessageAsync(
        int accountId,
        int folderId,
        MessageAdministrationSnapshot snapshot,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Message insertion is not available in this store.");

    ValueTask<bool> UpdateMessageAsync(
        int accountId,
        int folderId,
        MessageAdministrationSnapshot snapshot,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Message update is not available in this store.");

    ValueTask<bool> DeleteMessageAsync(
        int accountId,
        int folderId,
        long messageId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Message deletion is not available in this store.");

    ValueTask ClearMessagesAsync(
        int accountId,
        int folderId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Message clear is not available in this store.");
}
