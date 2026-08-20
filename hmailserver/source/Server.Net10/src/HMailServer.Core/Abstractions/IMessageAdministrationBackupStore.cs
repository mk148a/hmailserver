namespace HMailServer.Core.Abstractions;

public interface IMessageAdministrationBackupStore
{
    ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetFolderMessagesForBackupAsync(
        int accountId,
        int folderId,
        CancellationToken cancellationToken);
}
