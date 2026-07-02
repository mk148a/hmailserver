namespace HMailServer.Core.Abstractions;

public interface IRouteAddressAdministrationStore
{
    ValueTask<IReadOnlyList<RouteAddressAdministrationSnapshot>> GetRouteAddressesAsync(
        int routeId,
        CancellationToken cancellationToken);
}
