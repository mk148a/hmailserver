namespace HMailServer.Core.Abstractions;

public interface IServerStatusAdministrationStore
{
    ValueTask<ServerStatusSnapshot> GetStatusAsync(CancellationToken cancellationToken);
}
