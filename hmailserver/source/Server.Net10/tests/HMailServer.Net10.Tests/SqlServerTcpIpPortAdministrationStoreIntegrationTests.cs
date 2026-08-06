using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerTcpIpPortAdministrationStoreIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task StoreMutations_ExhibitLegacyIdentityReadbackOwnerScopingAndStatementRollback()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_tcpipport_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var store = new SqlServerTcpIpPortAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString));

            var insertedId = await store.InsertTcpIpPortAsync(
                new TcpIpPortAdministrationSnapshot(
                    Id: 0,
                    Protocol: 1,
                    PortNumber: 25,
                    Address: "0.0.0.0",
                    ConnectionSecurity: 0,
                    SslCertificateId: 0),
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(insertedId > 0);

            var readBack = await store.GetTcpIpPortsAsync(CancellationToken.None).ConfigureAwait(false);
            var inserted = readBack.Single(port => port.Id == insertedId);
            Assert.AreEqual(1, inserted.Protocol);
            Assert.AreEqual(25, inserted.PortNumber);
            Assert.AreEqual("0.0.0.0", inserted.Address);
            Assert.AreEqual(0, inserted.ConnectionSecurity);

            var secondId = await store.InsertTcpIpPortAsync(
                new TcpIpPortAdministrationSnapshot(
                    Id: 0,
                    Protocol: 3,
                    PortNumber: 143,
                    Address: "0.0.0.0",
                    ConnectionSecurity: 0,
                    SslCertificateId: 0),
                CancellationToken.None).ConfigureAwait(false);
            Assert.AreNotEqual(insertedId, secondId);
            Assert.AreEqual(4, (await store.GetTcpIpPortsAsync(CancellationToken.None).ConfigureAwait(false)).Count);

            await store.UpdateTcpIpPortAsync(
                new TcpIpPortAdministrationSnapshot(
                    Id: insertedId,
                    Protocol: 5,
                    PortNumber: 993,
                    Address: "0.0.0.0",
                    ConnectionSecurity: 3,
                    SslCertificateId: 1),
                CancellationToken.None).ConfigureAwait(false);
            var afterUpdate = (await store.GetTcpIpPortsAsync(CancellationToken.None).ConfigureAwait(false))
                .Single(port => port.Id == insertedId);
            Assert.AreEqual(5, afterUpdate.Protocol);
            Assert.AreEqual(993, afterUpdate.PortNumber);
            Assert.AreEqual(3, afterUpdate.ConnectionSecurity);

            await store.DeleteTcpIpPortByIdAsync(insertedId, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(3, (await store.GetTcpIpPortsAsync(CancellationToken.None).ConfigureAwait(false)).Count);

            await Assert.ThrowsExactlyAsync<SqlException>(
                () => store.InsertTcpIpPortAsync(
                    new TcpIpPortAdministrationSnapshot(
                        Id: 0,
                        Protocol: 3,
                        PortNumber: 110,
                        Address: "0.0.0.0",
                        ConnectionSecurity: 0,
                        SslCertificateId: 0),
                    CancellationToken.None).AsTask()).ConfigureAwait(false);
            Assert.AreEqual(3, (await store.GetTcpIpPortsAsync(CancellationToken.None).ConfigureAwait(false)).Count);

            await store.DeleteAllTcpIpPortsAsync(CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(0, (await store.GetTcpIpPortsAsync(CancellationToken.None).ConfigureAwait(false)).Count);
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
            CREATE TABLE dbo.hm_tcpipports (
                portid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                portprotocol tinyint NOT NULL,
                portnumber int NOT NULL,
                portaddress1 bigint NOT NULL,
                portaddress2 bigint NULL,
                portconnectionsecurity tinyint NOT NULL,
                portsslcertificateid bigint NOT NULL,
                CONSTRAINT u_hm_tcpipports_portnumber UNIQUE (portnumber)
            );
            INSERT INTO dbo.hm_tcpipports
                (portprotocol, portnumber, portaddress1, portaddress2, portconnectionsecurity, portsslcertificateid)
            VALUES
                (1, 587, 0, 0, 0, 0),
                (3, 110, 0, 0, 0, 0);
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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