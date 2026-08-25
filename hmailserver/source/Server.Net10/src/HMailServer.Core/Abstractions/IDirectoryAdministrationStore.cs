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

    ValueTask<bool> UpdateDataDirectoryAsync(
        string dataDirectory,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Data directory updates are not implemented by this store.");

    ValueTask<bool> UpdateProgramDirectoryAsync(
        string programDirectory,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Program directory updates are not implemented by this store.");

    ValueTask<bool> UpdateEventDirectoryAsync(
        string eventDirectory,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Event directory updates are not implemented by this store.");

    ValueTask<bool> UpdateDatabaseDirectoryAsync(
        string databaseDirectory,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Database directory updates are not implemented by this store.");
}
