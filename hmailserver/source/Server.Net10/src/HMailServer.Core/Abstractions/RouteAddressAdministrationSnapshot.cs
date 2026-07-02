namespace HMailServer.Core.Abstractions;

public sealed record RouteAddressAdministrationSnapshot(
    int Id,
    int RouteId,
    string Address);
