using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerRouteAdministrationStore : IRouteAdministrationStore
{
    public const string GetRoutesSql = """
        SELECT routeid, routedomainname, routedescription, routetargetsmthost, routetargetsmtport,
               routenooftries, routeminutesbetweentry, routealladdresses, routeuseauthentication,
               routeauthenticationusername, routetreatsecurityaslocal, routeconnectionsecurity,
               routetreatsenderaslocaldomain
        FROM hm_routes
        ORDER BY routedomainname ASC;
        """;

    public const string InsertRouteSql = """
        INSERT INTO hm_routes
            (routedomainname, routedescription, routetargetsmthost, routetargetsmtport,
             routenooftries, routeminutesbetweentry, routealladdresses, routeuseauthentication,
             routeauthenticationusername, routeauthenticationpassword, routetreatsecurityaslocal,
             routetreatsenderaslocaldomain, routeconnectionsecurity)
        OUTPUT INSERTED.routeid
        VALUES
            (@DomainName, @Description, @TargetSmtpHost, @TargetSmtpPort,
             @NumberOfTries, @MinutesBetweenTry, @AllAddresses, @RelayerRequiresAuth,
             @RelayerAuthUsername, @RelayerAuthPassword, @TreatRecipientAsLocalDomain,
             @TreatSenderAsLocalDomain, @ConnectionSecurity);
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
            CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);
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

    public async ValueTask<int> InsertRouteAsync(
        RouteAdministrationSnapshot route,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(route);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertRouteSql, connection);
        command.Parameters.Add("@DomainName", SqlDbType.NVarChar, 255).Value = route.DomainName;
        command.Parameters.Add("@Description", SqlDbType.NVarChar, 255).Value = route.Description;
        command.Parameters.Add("@TargetSmtpHost", SqlDbType.NVarChar, 255).Value = route.TargetSmtpHost;
        command.Parameters.Add("@TargetSmtpPort", SqlDbType.Int).Value = route.TargetSmtpPort;
        command.Parameters.Add("@NumberOfTries", SqlDbType.Int).Value = route.NumberOfTries;
        command.Parameters.Add("@MinutesBetweenTry", SqlDbType.Int).Value = route.MinutesBetweenTry;
        command.Parameters.Add("@AllAddresses", SqlDbType.TinyInt).Value = route.AllAddresses ? 1 : 0;
        command.Parameters.Add("@RelayerRequiresAuth", SqlDbType.TinyInt).Value = route.RelayerRequiresAuth ? 1 : 0;
        command.Parameters.Add("@RelayerAuthUsername", SqlDbType.NVarChar, 255).Value = route.RelayerAuthUsername;
        command.Parameters.Add("@RelayerAuthPassword", SqlDbType.NVarChar, 255).Value =
            LegacyBlowfishPasswordCipher.Encrypt(route.RelayerAuthPassword);
        command.Parameters.Add("@TreatRecipientAsLocalDomain", SqlDbType.TinyInt).Value =
            route.TreatRecipientAsLocalDomain ? 1 : 0;
        command.Parameters.Add("@TreatSenderAsLocalDomain", SqlDbType.TinyInt).Value =
            route.TreatSenderAsLocalDomain ? 1 : 0;
        command.Parameters.Add("@ConnectionSecurity", SqlDbType.TinyInt).Value = route.ConnectionSecurity;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, CultureInfo.InvariantCulture);
    }

    private static bool ReadLegacyBoolean(SqlDataReader reader, int ordinal) =>
        Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
}