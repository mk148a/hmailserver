namespace HMailServer.Core.Abstractions;

public interface IImapFolderAdministrationMutationStore
{
    ValueTask<ImapFolderAdministrationSnapshot> InsertFolderAsync(
        int accountId,
        int parentFolderId,
        string encodedName,
        bool subscribed,
        CancellationToken cancellationToken);
}
