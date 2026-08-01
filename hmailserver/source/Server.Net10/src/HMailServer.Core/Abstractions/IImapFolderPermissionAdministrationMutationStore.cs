namespace HMailServer.Core.Abstractions;

public interface IImapFolderPermissionAdministrationMutationStore
{
    ValueTask<bool> UpdateFolderPermissionAsync(
        int folderId,
        int permissionId,
        int permissionType,
        int permissionGroupId,
        int permissionAccountId,
        int value,
        CancellationToken cancellationToken);

    ValueTask<ImapFolderPermissionAdministrationSnapshot?> InsertFolderPermissionAsync(
        int folderId,
        int permissionType,
        int permissionGroupId,
        int permissionAccountId,
        int value,
        CancellationToken cancellationToken);
}
