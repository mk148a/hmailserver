namespace HMailServer.Core.Abstractions;

public interface IMessageAdministrationStore
{
    ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetAccountMessagesAsync(
        int accountId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetFolderMessagesAsync(
        int folderId,
        CancellationToken cancellationToken);
}
