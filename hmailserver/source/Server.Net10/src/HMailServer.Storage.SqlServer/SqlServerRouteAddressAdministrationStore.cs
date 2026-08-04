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

    public const string DeleteRouteAddressByIdSql = """
DELETE FROM hm_routeaddresses
WHERE routeaddressrouteid = @RouteId
  AND routeaddressid = @Id;
""";

    public const string InsertRouteAddressSql = """
INSERT INTO hm_routeaddresses
    (routeaddressrouteid, routeaddressaddress)
OUTPUT INSERTED.routeaddressid
VALUES (@RouteId, @Address);
""";

    public const string UpdateRouteAddressSql = """
UPDATE hm_routeaddresses
SET routeaddressrouteid = @TargetRouteId,
    routeaddressaddress = @Address
WHERE routeaddressrouteid = @OwningRouteId
  AND routeaddressid = @Id;
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

    public async ValueTask DeleteRouteAddressByIdAsync(
        int routeId,
        int databaseId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteRouteAddressByIdSql, connection);
        command.Parameters.Add("@RouteId", SqlDbType.Int).Value = routeId;
        command.Parameters.Add("@Id", SqlDbType.Int).Value = databaseId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<int> InsertRouteAddressAsync(
        int owningRouteId,
        RouteAddressAdministrationSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertRouteAddressSql, connection);
        command.Parameters.Add("@RouteId", SqlDbType.Int).Value = owningRouteId;
        command.Parameters.Add("@Address", SqlDbType.NVarChar, 255).Value = snapshot.Address;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async ValueTask<bool> UpdateRouteAddressAsync(
        int owningRouteId,
        RouteAddressAdministrationSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateRouteAddressSql, connection);
        command.Parameters.Add("@TargetRouteId", SqlDbType.Int).Value = snapshot.RouteId;
        command.Parameters.Add("@OwningRouteId", SqlDbType.Int).Value = owningRouteId;
        command.Parameters.Add("@Id", SqlDbType.Int).Value = snapshot.Id;
        command.Parameters.Add("@Address", SqlDbType.NVarChar, 255).Value = snapshot.Address;
        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affectedRows == 1;
    }
}
