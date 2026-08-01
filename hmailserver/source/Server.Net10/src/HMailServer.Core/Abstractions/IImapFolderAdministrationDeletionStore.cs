namespace HMailServer.Core.Abstractions;

public interface IImapFolderAdministrationDeletionStore
{
    ValueTask<ImapFolderAdministrationDeletionResult> DeleteFolderAsync(
        ImapFolderAdministrationSnapshot folder,
        CancellationToken cancellationToken);
}

public sealed record ImapFolderAdministrationDeletionResult(
    bool Succeeded,
    IReadOnlyList<ImapFolderAdministrationDeletedMessage> DeletedMessages);

public sealed record ImapFolderAdministrationDeletedMessage(
    string FileName,
    int AccountId,
    int FolderId,
    string? AccountAddress,
    int MessageType);
