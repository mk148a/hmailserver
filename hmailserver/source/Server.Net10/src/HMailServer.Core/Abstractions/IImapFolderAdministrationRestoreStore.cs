namespace HMailServer.Core.Abstractions;

public interface IImapFolderAdministrationRestoreStore
{
    ValueTask<ImapFolderAdministrationSnapshot> InsertFolderForRestoreAsync(
        ImapFolderAdministrationSnapshot folder,
        CancellationToken cancellationToken);
}
