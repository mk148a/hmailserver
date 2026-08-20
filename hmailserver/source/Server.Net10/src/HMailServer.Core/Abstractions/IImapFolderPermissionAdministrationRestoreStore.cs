namespace HMailServer.Core.Abstractions;

public interface IImapFolderPermissionAdministrationRestoreStore
{
    ValueTask<ImapFolderPermissionAdministrationSnapshot?> InsertFolderPermissionForRestoreAsync(
        int folderId,
        int permissionType,
        int permissionGroupId,
        int permissionAccountId,
        int value,
        CancellationToken cancellationToken);
}
