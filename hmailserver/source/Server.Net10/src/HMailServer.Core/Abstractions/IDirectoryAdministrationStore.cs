namespace HMailServer.Core.Abstractions;

public interface IDirectoryAdministrationStore
{
    ValueTask<DirectoryAdministrationSnapshot> GetDirectoriesAsync(CancellationToken cancellationToken);

    ValueTask<bool> UpdateLogDirectoryAsync(
        string logDirectory,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Log directory updates are not implemented by this store.");
}
