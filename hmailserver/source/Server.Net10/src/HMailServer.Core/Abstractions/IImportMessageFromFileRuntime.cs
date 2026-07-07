namespace HMailServer.Core.Abstractions;

public interface IImportMessageFromFileRuntime
{
    ValueTask<bool> ImportMessageFromFileAsync(
        string fileName,
        int accountId,
        CancellationToken cancellationToken);

    ValueTask<bool> ImportMessageFromFileToImapFolderAsync(
        string fileName,
        int accountId,
        string imapFolder,
        CancellationToken cancellationToken);
}
