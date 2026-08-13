namespace HMailServer.Core.Abstractions;

public interface IImapFolderAdministrationDeletionStore
{
    ValueTask<ImapFolderAdministrationDeletionResult> DeleteFolderAsync(
        ImapFolderAdministrationSnapshot folder,
        CancellationToken cancellationToken);

    ValueTask<ImapFolderAdministrationDeletionResult> DeleteAllForAccountAsync(
        int accountId,
        int domainId,
        string accountAddress,
        CancellationToken cancellationToken);
}

public interface IImapFolderMessageFileDeletionRuntime
{
    bool TryDeleteAll(ImapFolderAdministrationDeletionResult result);
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
