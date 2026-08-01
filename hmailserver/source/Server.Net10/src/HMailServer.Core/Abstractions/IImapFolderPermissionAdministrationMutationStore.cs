namespace HMailServer.Core.Abstractions;

public interface IImapFolderPermissionAdministrationMutationStore
{
    ValueTask<ImapFolderPermissionAdministrationSnapshot?> InsertFolderPermissionAsync(
        int folderId,
        int permissionType,
        int permissionGroupId,
        int permissionAccountId,
        int value,
        CancellationToken cancellationToken);
}
