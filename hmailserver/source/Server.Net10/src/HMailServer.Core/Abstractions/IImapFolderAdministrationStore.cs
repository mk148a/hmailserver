namespace HMailServer.Core.Abstractions;

public interface IImapFolderAdministrationStore
{
    ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetFoldersForAccountAsync(
        int accountId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetRootFoldersAsync(
        int accountId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetChildFoldersAsync(
        int parentFolderId,
        int accountId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>> GetFolderPermissionsAsync(
        int folderId,
        CancellationToken cancellationToken);
}
