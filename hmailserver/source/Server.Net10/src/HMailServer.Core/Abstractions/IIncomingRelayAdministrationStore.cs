namespace HMailServer.Core.Abstractions;

public interface IIncomingRelayAdministrationStore
{
    ValueTask<IReadOnlyList<IncomingRelayAdministrationSnapshot>> GetIncomingRelaysAsync(
        CancellationToken cancellationToken);

    ValueTask DeleteIncomingRelayByIdAsync(
        int databaseId,
        CancellationToken cancellationToken);

    ValueTask UpdateIncomingRelayAsync(
        IncomingRelayAdministrationSnapshot relay,
        CancellationToken cancellationToken);

    ValueTask<int> InsertIncomingRelayAsync(
        IncomingRelayAdministrationSnapshot relay,
        CancellationToken cancellationToken);
}
