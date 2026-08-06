using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerRouteAdministrationStoreIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task StoreMutations_ExhibitLegacyIdentityReadbackOwnerScopingAndStatementRollback()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_route_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var store = new SqlServerRouteAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString));

            var insertedId = await store.InsertRouteAsync(
                new RouteAdministrationSnapshot(
                    Id: 0,
                    DomainName: "alpha.example",
                    Description: "Alpha",
                    TargetSmtpHost: "smtp.alpha.example",
                    TargetSmtpPort: 2525,
                    NumberOfTries: 4,
                    MinutesBetweenTry: 15,
                    AllAddresses: true,
                    RelayerRequiresAuth: true,
                    RelayerAuthUsername: "relay-user",
                    TreatRecipientAsLocalDomain: true,
                    TreatSenderAsLocalDomain: false,
                    ConnectionSecurity: 3,
                    RelayerAuthPassword: "secret"),
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(insertedId > 0);

            var readBack = await store.GetRoutesAsync(CancellationToken.None).ConfigureAwait(false);
            var inserted = readBack.Single(route => route.Id == insertedId);
            Assert.AreEqual("alpha.example", inserted.DomainName);
            Assert.AreEqual("Alpha", inserted.Description);
            Assert.AreEqual("smtp.alpha.example", inserted.TargetSmtpHost);
            Assert.AreEqual(2525, inserted.TargetSmtpPort);
            Assert.AreEqual(4, inserted.NumberOfTries);
            Assert.AreEqual(15, inserted.MinutesBetweenTry);
            Assert.IsTrue(inserted.AllAddresses);
            Assert.IsTrue(inserted.RelayerRequiresAuth);
            Assert.AreEqual("relay-user", inserted.RelayerAuthUsername);
            Assert.IsTrue(inserted.TreatRecipientAsLocalDomain);
            Assert.IsFalse(inserted.TreatSenderAsLocalDomain);
            Assert.AreEqual(3, inserted.ConnectionSecurity);
            Assert.IsTrue(
                LegacyBlowfishPasswordCipher.TryDecrypt(
                    await ReadEncryptedPasswordAsync(testConnectionString, insertedId).ConfigureAwait(false),
                    out var decryptedPassword));
            Assert.AreEqual("secret", decryptedPassword);

            var secondId = await store.InsertRouteAsync(
                new RouteAdministrationSnapshot(
                    Id: 0,
                    DomainName: "beta.example",
                    Description: string.Empty,
                    TargetSmtpHost: "smtp.beta.example",
                    TargetSmtpPort: 25,
                    NumberOfTries: 3,
                    MinutesBetweenTry: 60,
                    AllAddresses: false,
                    RelayerRequiresAuth: false,
                    RelayerAuthUsername: string.Empty,
                    TreatRecipientAsLocalDomain: false,
                    TreatSenderAsLocalDomain: true,
                    ConnectionSecurity: 1),
                CancellationToken.None).ConfigureAwait(false);
            Assert.AreNotEqual(insertedId, secondId);
            Assert.AreEqual(3, (await store.GetRoutesAsync(CancellationToken.None).ConfigureAwait(false)).Count);

            var unknownUpdate = await store.UpdateRouteAsync(
                new RouteAdministrationSnapshot(
                    Id: 9999,
                    DomainName: "renamed.example",
                    Description: string.Empty,
                    TargetSmtpHost: string.Empty,
                    TargetSmtpPort: 0,
                    NumberOfTries: 0,
                    MinutesBetweenTry: 0,
                    AllAddresses: false,
                    RelayerRequiresAuth: false,
                    RelayerAuthUsername: string.Empty,
                    TreatRecipientAsLocalDomain: false,
                    TreatSenderAsLocalDomain: false,
                    ConnectionSecurity: 0),
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(unknownUpdate);
            Assert.AreEqual(
                "alpha.example",
                (await store.GetRoutesAsync(CancellationToken.None).ConfigureAwait(false))
                    .Single(route => route.Id == insertedId).DomainName);

            var ownUpdate = await store.UpdateRouteAsync(
                new RouteAdministrationSnapshot(
                    Id: insertedId,
                    DomainName: "renamed.example",
                    Description: "Renamed",
                    TargetSmtpHost: "smtp.renamed.example",
                    TargetSmtpPort: 26,
                    NumberOfTries: 2,
                    MinutesBetweenTry: 30,
                    AllAddresses: false,
                    RelayerRequiresAuth: true,
                    RelayerAuthUsername: "relay-user",
                    TreatRecipientAsLocalDomain: true,
                    TreatSenderAsLocalDomain: true,
                    ConnectionSecurity: 2,
                    RelayerAuthPassword: "new-secret"),
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(ownUpdate);
            var afterUpdate = (await store.GetRoutesAsync(CancellationToken.None).ConfigureAwait(false))
                .Single(route => route.Id == insertedId);
            Assert.AreEqual(insertedId, afterUpdate.Id);
            Assert.AreEqual("renamed.example", afterUpdate.DomainName);
            Assert.AreEqual("Renamed", afterUpdate.Description);
            Assert.AreEqual("smtp.renamed.example", afterUpdate.TargetSmtpHost);
            Assert.AreEqual(26, afterUpdate.TargetSmtpPort);
            Assert.AreEqual(2, afterUpdate.NumberOfTries);
            Assert.AreEqual(30, afterUpdate.MinutesBetweenTry);
            Assert.IsFalse(afterUpdate.AllAddresses);
            Assert.IsTrue(afterUpdate.TreatRecipientAsLocalDomain);
            Assert.IsTrue(afterUpdate.TreatSenderAsLocalDomain);
            Assert.AreEqual(2, afterUpdate.ConnectionSecurity);
            Assert.IsTrue(
                LegacyBlowfishPasswordCipher.TryDecrypt(
                    await ReadEncryptedPasswordAsync(testConnectionString, insertedId).ConfigureAwait(false),
                    out var updatedPassword));
            Assert.AreEqual("new-secret", updatedPassword);

            var unknownDelete = await store.DeleteRouteByIdAsync(9999, CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(unknownDelete);
            Assert.AreEqual(3, (await store.GetRoutesAsync(CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(1, await CountRouteAddressRowsAsync(testConnectionString, 1).ConfigureAwait(false));

            var cascadeDelete = await store.DeleteRouteByIdAsync(1, CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(cascadeDelete);
            Assert.AreEqual(0, await CountRouteAddressRowsAsync(testConnectionString, 1).ConfigureAwait(false));
            Assert.AreEqual(2, (await store.GetRoutesAsync(CancellationToken.None).ConfigureAwait(false)).Count);

            var ownDelete = await store.DeleteRouteByIdAsync(insertedId, CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(ownDelete);
            Assert.AreEqual(1, (await store.GetRoutesAsync(CancellationToken.None).ConfigureAwait(false)).Count);

            await Assert.ThrowsExactlyAsync<SqlException>(
                () => store.InsertRouteAsync(
                    new RouteAdministrationSnapshot(
                        Id: 0,
                        DomainName: null!,
                        Description: string.Empty,
                        TargetSmtpHost: string.Empty,
                        TargetSmtpPort: 0,
                        NumberOfTries: 0,
                        MinutesBetweenTry: 0,
                        AllAddresses: false,
                        RelayerRequiresAuth: false,
                        RelayerAuthUsername: string.Empty,
                        TreatRecipientAsLocalDomain: false,
                        TreatSenderAsLocalDomain: false,
                        ConnectionSecurity: 0),
                    CancellationToken.None).AsTask()).ConfigureAwait(false);
            Assert.AreEqual(1, (await store.GetRoutesAsync(CancellationToken.None).ConfigureAwait(false)).Count);

            await Assert.ThrowsExactlyAsync<SqlException>(
                () => store.UpdateRouteAsync(
                    new RouteAdministrationSnapshot(
                        Id: secondId,
                        DomainName: null!,
                        Description: string.Empty,
                        TargetSmtpHost: string.Empty,
                        TargetSmtpPort: 0,
                        NumberOfTries: 0,
                        MinutesBetweenTry: 0,
                        AllAddresses: false,
                        RelayerRequiresAuth: false,
                        RelayerAuthUsername: string.Empty,
                        TreatRecipientAsLocalDomain: false,
                        TreatSenderAsLocalDomain: false,
                        ConnectionSecurity: 0),
                    CancellationToken.None).AsTask()).ConfigureAwait(false);
            var afterFailedUpdate = (await store.GetRoutesAsync(CancellationToken.None).ConfigureAwait(false))
                .Single(route => route.Id == secondId);
            Assert.AreEqual("beta.example", afterFailedUpdate.DomainName);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    private static string GetApprovedConnectionStringOrInconclusive()
    {
        var rawConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        var allowDatabaseCreate = Environment.GetEnvironmentVariable(AllowDatabaseCreateEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(rawConnectionString) || !string.Equals(allowDatabaseCreate, "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive(
                $"Set {ConnectionEnvironmentVariable} to a disposable local SQL target and " +
                $"{AllowDatabaseCreateEnvironmentVariable}=1 to run this destructive fixture.");
        }

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(rawConnectionString);
        }
        catch (ArgumentException exception)
        {
            Assert.Inconclusive($"The SQL integration connection string is invalid: {exception.Message}");
            throw;
        }

        if (!IsApprovedLocalDataSource(builder.DataSource) || !string.IsNullOrWhiteSpace(builder.AttachDBFilename))
        {
            Assert.Inconclusive(
                "The SQL integration fixture only accepts a local SQL/LocalDB target without AttachDbFilename.");
        }

        return builder.ConnectionString;
    }

    private static bool IsApprovedLocalDataSource(string dataSource)
    {
        var normalized = dataSource.Trim();
        return normalized.Equals(".", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("(local)", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("::1", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".\\", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("localhost\\", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("127.0.0.1,", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("localhost,", StringComparison.OrdinalIgnoreCase);
    }

    private static string WithDatabase(string connectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = databaseName };
        return builder.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand($"CREATE DATABASE [{databaseName}]", connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task CreateSchemaAndSeedAsync(string connectionString)
    {
        const string sql = """
            CREATE TABLE dbo.hm_routes (
                routeid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                routedomainname nvarchar(255) NOT NULL,
                routedescription nvarchar(255) NOT NULL,
                routetargetsmthost nvarchar(255) NOT NULL,
                routetargetsmtport int NOT NULL,
                routenooftries int NOT NULL,
                routeminutesbetweentry int NOT NULL,
                routealladdresses tinyint NOT NULL,
                routeuseauthentication tinyint NOT NULL,
                routeauthenticationusername nvarchar(255) NOT NULL,
                routeauthenticationpassword nvarchar(255) NOT NULL,
                routetreatsecurityaslocal tinyint NOT NULL,
                routeconnectionsecurity tinyint NOT NULL,
                routetreatsenderaslocaldomain tinyint NOT NULL
            );
            CREATE TABLE dbo.hm_routeaddresses (
                routeaddressid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                routeaddressrouteid int NOT NULL,
                routeaddressaddress nvarchar(255) NOT NULL
            );
            CREATE INDEX idx_hm_routeaddresses_routeaddressrouteid ON dbo.hm_routeaddresses (routeaddressrouteid);
            INSERT INTO dbo.hm_routes
                (routedomainname, routedescription, routetargetsmthost, routetargetsmtport,
                 routenooftries, routeminutesbetweentry, routealladdresses, routeuseauthentication,
                 routeauthenticationusername, routeauthenticationpassword, routetreatsecurityaslocal,
                 routeconnectionsecurity, routetreatsenderaslocaldomain)
            VALUES
                (N'seeded.example', N'', N'smtp.seeded.example', 25, 3, 60, 1, 0, N'', N'', 0, 0, 0);
            INSERT INTO dbo.hm_routeaddresses (routeaddressrouteid, routeaddressaddress)
            VALUES (1, N'routeaddress@example.test');
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<string> ReadEncryptedPasswordAsync(string connectionString, int routeId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT routeauthenticationpassword FROM dbo.hm_routes WHERE routeid = @RouteId;",
            connection);
        command.Parameters.Add("@RouteId", SqlDbType.Int).Value = routeId;
        var value = await command.ExecuteScalarAsync().ConfigureAwait(false);
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task<long> CountRouteAddressRowsAsync(string connectionString, int routeId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.hm_routeaddresses WHERE routeaddressrouteid = @RouteId;",
            connection);
        command.Parameters.Add("@RouteId", SqlDbType.Int).Value = routeId;
        return Convert.ToInt64(
            await command.ExecuteScalarAsync().ConfigureAwait(false),
            CultureInfo.InvariantCulture);
    }

    private static async Task DropDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];",
            connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}