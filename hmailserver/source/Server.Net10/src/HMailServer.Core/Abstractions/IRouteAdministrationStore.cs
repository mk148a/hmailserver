namespace HMailServer.Core.Abstractions;

public interface IRouteAdministrationStore
{
    ValueTask<IReadOnlyList<RouteAdministrationSnapshot>> GetRoutesAsync(
        CancellationToken cancellationToken);
}
