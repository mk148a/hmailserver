namespace HMailServer.Core.Abstractions;

public interface ITcpIpPortAdministrationStore
{
    ValueTask<IReadOnlyList<TcpIpPortAdministrationSnapshot>> GetTcpIpPortsAsync(
        CancellationToken cancellationToken);

    ValueTask<int> InsertTcpIpPortAsync(
        TcpIpPortAdministrationSnapshot port,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("TCP/IP port insertion is not implemented by this store.");

    ValueTask DeleteTcpIpPortByIdAsync(
        int databaseId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("TCP/IP port deletion is not implemented by this store.");

    ValueTask UpdateTcpIpPortAsync(
        TcpIpPortAdministrationSnapshot port,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("TCP/IP port updates are not implemented by this store.");
}
