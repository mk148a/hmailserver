namespace HMailServer.Core.Abstractions;

public interface IDirectoryAdministrationStore
{
    ValueTask<DirectoryAdministrationSnapshot> GetDirectoriesAsync(CancellationToken cancellationToken);

    ValueTask<bool> UpdateLogDirectoryAsync(
        string logDirectory,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Log directory updates are not implemented by this store.");

    ValueTask<bool> UpdateTempDirectoryAsync(
        string tempDirectory,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Temp directory updates are not implemented by this store.");
}
