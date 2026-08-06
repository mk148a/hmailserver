namespace HMailServer.Core.Abstractions;

public interface IRouteAdministrationStore
{
    ValueTask<IReadOnlyList<RouteAdministrationSnapshot>> GetRoutesAsync(
        CancellationToken cancellationToken);

    ValueTask<int> InsertRouteAsync(
        RouteAdministrationSnapshot snapshot,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Route insertion is not available in this store.");
}