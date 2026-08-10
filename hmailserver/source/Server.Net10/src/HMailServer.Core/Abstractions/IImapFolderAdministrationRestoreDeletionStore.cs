namespace HMailServer.Core.Abstractions;

public interface IImapFolderAdministrationRestoreDeletionStore
{
    ValueTask<bool> DeleteRestoredFolderTreeAsync(
        int accountId,
        int folderId,
        int parentFolderId,
        CancellationToken cancellationToken);
}
