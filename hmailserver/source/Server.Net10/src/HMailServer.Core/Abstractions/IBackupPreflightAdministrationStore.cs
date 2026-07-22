namespace HMailServer.Core.Abstractions;

public interface IBackupPreflightAdministrationStore
{
    ValueTask<bool> AreAllMessageFilesInDataDirectoryAsync(
        string dataDirectory,
        CancellationToken cancellationToken);
}
