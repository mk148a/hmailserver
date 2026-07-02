namespace HMailServer.Core.Abstractions;

public interface IImapFolderAdministrationStore
{
    ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetRootFoldersAsync(
        int accountId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>> GetFolderPermissionsAsync(
        int folderId,
        CancellationToken cancellationToken);
}
