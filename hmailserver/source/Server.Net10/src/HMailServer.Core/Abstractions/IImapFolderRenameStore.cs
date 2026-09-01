namespace HMailServer.Core.Abstractions;

public interface IImapFolderRenameStore
{
    ValueTask<ImapFolderRenameResult> RenameRootFolderAsync(
        int accountId,
        string sourceName,
        string destinationName,
        CancellationToken cancellationToken);
}

public enum ImapFolderRenameStatus
{
    Success,
    FolderNotFound,
    TargetExists,
    PermissionDenied,
    Failed
}

public sealed record ImapFolderRenameResult(
    ImapFolderRenameStatus Status,
    ImapFolderAdministrationSnapshot? Folder = null);
