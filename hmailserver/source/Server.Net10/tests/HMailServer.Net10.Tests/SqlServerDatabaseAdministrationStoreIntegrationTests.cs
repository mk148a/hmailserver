using System.Data;
using System.Globalization;
using HMailServer.Security;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerDatabaseAdministrationStoreIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task VersionGate_AndUpgradeRollback_AreReadBackFromIsolatedDatabase()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_dbver_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAsync(testConnectionString).ConfigureAwait(false);
            await SetVersionAsync(testConnectionString, 5000).ConfigureAwait(false);

            var store = new SqlServerDatabaseAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString),
                new LegacyDatabaseConfiguration(
                    DatabaseType: 2,
                    DatabaseExists: true,
                    ServerName: string.Empty,
                    DatabaseName: databaseName));

            var before = await store.GetDatabaseAsync(CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(5708, before.RequiredVersion);
            Assert.AreEqual(5000, before.CurrentVersion);
            Assert.IsTrue(before.CurrentVersion < before.RequiredVersion);
            Assert.IsTrue(before.IsConnected);
            Assert.IsTrue(before.DatabaseExists);

            await SetVersionAsync(testConnectionString, 5708).ConfigureAwait(false);
            var upgraded = await store.GetDatabaseAsync(CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(5708, upgraded.CurrentVersion);
            Assert.IsFalse(upgraded.CurrentVersion < upgraded.RequiredVersion);

            await SetVersionAsync(testConnectionString, 5000).ConfigureAwait(false);
            var rolledBack = await store.GetDatabaseAsync(CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(5000, rolledBack.CurrentVersion);
            Assert.IsTrue(rolledBack.CurrentVersion < rolledBack.RequiredVersion);
            Assert.AreEqual(1, await CountVersionRowsAsync(testConnectionString).ConfigureAwait(false));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task DatabaseAdministrationTransaction_CanBeginCommitAndRollbackOnIsolatedDatabase()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_dbtx_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            var store = new SqlServerDatabaseAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString),
                new LegacyDatabaseConfiguration(
                    DatabaseType: 2,
                    DatabaseExists: true,
                    ServerName: string.Empty,
                    DatabaseName: databaseName));

            await using (var committed = await store.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false))
            {
                var commitScript = Path.Combine(Path.GetTempPath(), $"hmailserver-net10-{Guid.NewGuid():N}.sql");
                await File.WriteAllTextAsync(commitScript, "CREATE TABLE dbo.hm_net10_transaction_probe (probeid int NOT NULL)").ConfigureAwait(false);
                try
                {
                    await committed.ExecuteScriptAsync(commitScript, CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    File.Delete(commitScript);
                }
                await committed.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await using (var rolledBack = await store.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false))
            {
                var rollbackScript = Path.Combine(Path.GetTempPath(), $"hmailserver-net10-{Guid.NewGuid():N}.sql");
                await File.WriteAllTextAsync(rollbackScript, "CREATE TABLE dbo.hm_net10_transaction_rollback_probe (probeid int NOT NULL)").ConfigureAwait(false);
                try
                {
                    await rolledBack.ExecuteScriptAsync(rollbackScript, CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    File.Delete(rollbackScript);
                }
                await rolledBack.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await using (var verification = new SqlConnection(testConnectionString))
            {
                await verification.OpenAsync().ConfigureAwait(false);
                await using var command = new SqlCommand(
                    "SELECT OBJECT_ID('dbo.hm_net10_transaction_probe'), OBJECT_ID('dbo.hm_net10_transaction_rollback_probe')",
                    verification);
                await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
                Assert.IsTrue(await reader.ReadAsync().ConfigureAwait(false));
                Assert.AreNotEqual(DBNull.Value, reader.GetValue(0));
                Assert.AreEqual(DBNull.Value, reader.GetValue(1));
            }
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

    private static async Task CreateSchemaAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "CREATE TABLE dbo.hm_dbversion ([value] int NOT NULL);",
            connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task SetVersionAsync(string connectionString, int version)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "DELETE FROM dbo.hm_dbversion; INSERT INTO dbo.hm_dbversion ([value]) VALUES (@Version);",
            connection);
        command.Parameters.Add("@Version", SqlDbType.Int).Value = version;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<long> CountVersionRowsAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM dbo.hm_dbversion;",
            connection);
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
