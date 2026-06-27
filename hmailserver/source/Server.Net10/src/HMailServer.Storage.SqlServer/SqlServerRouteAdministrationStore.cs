using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerRouteAdministrationStore : IRouteAdministrationStore
{
    public const string GetRoutesSql = """
SELECT
    routeid,
    routedomainname,
    routedescription,
    routetargetsmthost,
    routetargetsmtport,
    routenooftries,
    routeminutesbetweentry,
    routealladdresses,
    routeuseauthentication,
    routeauthenticationusername,
    routetreatsecurityaslocal,
    routeconnectionsecurity,
    routetreatsenderaslocaldomain
FROM hm_routes
ORDER BY routedomainname ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerRouteAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<RouteAdministrationSnapshot>> GetRoutesAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetRoutesSql, connection);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var routes = new List<RouteAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            routes.Add(
                new RouteAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    DomainName: reader.GetString(1),
                    Description: reader.GetString(2),
                    TargetSmtpHost: reader.GetString(3),
                    TargetSmtpPort: reader.GetInt32(4),
                    NumberOfTries: reader.GetInt32(5),
                    MinutesBetweenTry: reader.GetInt32(6),
                    AllAddresses: ReadLegacyBoolean(reader, 7),
                    RelayerRequiresAuth: ReadLegacyBoolean(reader, 8),
                    RelayerAuthUsername: reader.GetString(9),
                    TreatRecipientAsLocalDomain: ReadLegacyBoolean(reader, 10),
                    ConnectionSecurity: Convert.ToInt32(reader.GetValue(11), CultureInfo.InvariantCulture),
                    TreatSenderAsLocalDomain: ReadLegacyBoolean(reader, 12)));
        }

        return routes;
    }

    private static bool ReadLegacyBoolean(SqlDataReader reader, int ordinal) =>
        Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
}
