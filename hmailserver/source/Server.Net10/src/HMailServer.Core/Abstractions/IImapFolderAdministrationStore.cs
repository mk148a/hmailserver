namespace HMailServer.Core.Abstractions;

public interface IImapFolderAdministrationStore
{
    ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetRootFoldersAsync(
        int accountId,
        CancellationToken cancellationToken);
}
