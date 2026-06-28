namespace HMailServer.Core.Abstractions;

public interface IDirectoryAdministrationStore
{
    ValueTask<DirectoryAdministrationSnapshot> GetDirectoriesAsync(CancellationToken cancellationToken);
}
