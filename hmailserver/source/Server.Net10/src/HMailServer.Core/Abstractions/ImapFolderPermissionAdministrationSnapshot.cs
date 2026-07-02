namespace HMailServer.Core.Abstractions;

public sealed record ImapFolderPermissionAdministrationSnapshot(
    int Id,
    int ShareFolderId,
    int PermissionType,
    int PermissionGroupId,
    int PermissionAccountId,
    int Value);
