namespace HMailServer.Core.Abstractions;

public interface ITcpIpPortAdministrationStore
{
    ValueTask<IReadOnlyList<TcpIpPortAdministrationSnapshot>> GetTcpIpPortsAsync(
        CancellationToken cancellationToken);
}
