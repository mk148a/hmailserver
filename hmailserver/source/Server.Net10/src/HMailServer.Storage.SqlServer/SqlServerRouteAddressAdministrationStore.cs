using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerRouteAddressAdministrationStore : IRouteAddressAdministrationStore
{
    public const string GetRouteAddressesSql = """
SELECT
    routeaddressid,
    routeaddressrouteid,
    routeaddressaddress
FROM hm_routeaddresses
WHERE routeaddressrouteid = @RouteId;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerRouteAddressAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<RouteAddressAdministrationSnapshot>> GetRouteAddressesAsync(
        int routeId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetRouteAddressesSql, connection);
        command.Parameters.Add("@RouteId", SqlDbType.Int).Value = routeId;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var addresses = new List<RouteAddressAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            addresses.Add(
                new RouteAddressAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    RouteId: reader.GetInt32(1),
                    Address: reader.GetString(2)));
        }

        return addresses;
    }
}
