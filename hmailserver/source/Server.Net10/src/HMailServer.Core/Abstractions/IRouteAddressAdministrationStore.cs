namespace HMailServer.Core.Abstractions;

public interface IRouteAddressAdministrationStore
{
    ValueTask<IReadOnlyList<RouteAddressAdministrationSnapshot>> GetRouteAddressesAsync(
        int routeId,
        CancellationToken cancellationToken);

    ValueTask DeleteRouteAddressByIdAsync(
        int routeId,
        int databaseId,
        CancellationToken cancellationToken);

    ValueTask<int> InsertRouteAddressAsync(
        int owningRouteId,
        RouteAddressAdministrationSnapshot snapshot,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateRouteAddressAsync(
        int owningRouteId,
        RouteAddressAdministrationSnapshot snapshot,
        CancellationToken cancellationToken);
}
