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


    public const string UpdateRouteSql = """
        UPDATE hm_routes
        SET routedomainname = @DomainName,
            routedescription = @Description,
            routetargetsmthost = @TargetSmtpHost,
            routetargetsmtport = @TargetSmtpPort,
            routenooftries = @NumberOfTries,
            routeminutesbetweentry = @MinutesBetweenTry,
            routealladdresses = @AllAddresses,
            routeuseauthentication = @RelayerRequiresAuth,
            routeauthenticationusername = @RelayerAuthUsername,
            routeauthenticationpassword = @RelayerAuthPassword,
            routetreatsecurityaslocal = @TreatRecipientAsLocalDomain,
            routetreatsenderaslocaldomain = @TreatSenderAsLocalDomain,
            routeconnectionsecurity = @ConnectionSecurity
        WHERE routeid = @ID;
        """;

    public const string DeleteRouteAddressesByRouteSql = """
        DELETE FROM hm_routeaddresses
        WHERE routeaddressrouteid = @RouteId;
        """;

    public const string DeleteRouteByIdSql = """
        DELETE FROM hm_routes
        WHERE routeid = @ID;
        """;

    public const string QueueMessagesForRouteSql = """
        UPDATE hm_messages
        SET messageaccountid = 0,
            messagetype = 1,
            messagenexttrytime = @NextTryTime
        WHERE messagetype = 3
          AND messageaccountid = @RouteID;
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

    public async ValueTask<bool> UpdateRouteAsync(
        RouteAdministrationSnapshot route,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(route);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateRouteSql, connection);
        command.Parameters.Add("@ID", SqlDbType.Int).Value = route.Id;
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
        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected == 1;
    }
    public async ValueTask<bool> DeleteRouteByIdAsync(
        int routeId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteRouteAddressesByRouteSql, connection);
        command.Parameters.Add("@RouteId", SqlDbType.Int).Value = routeId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var deleteCommand = new SqlCommand(DeleteRouteByIdSql, connection);
        deleteCommand.Parameters.Add("@ID", SqlDbType.Int).Value = routeId;
        var affected = await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected == 1;
    }

    public async ValueTask<bool> QueueMessagesForRouteAsync(
        int routeId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(QueueMessagesForRouteSql, connection);
        command.Parameters.Add("@RouteID", SqlDbType.Int).Value = routeId;
        command.Parameters.Add("@NextTryTime", SqlDbType.DateTime).Value = new DateTime(1901, 1, 1);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
    private static bool ReadLegacyBoolean(SqlDataReader reader, int ordinal) =>
        Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
}
