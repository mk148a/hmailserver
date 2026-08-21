using System.Data;
using System.Globalization;
using System.Text.Json;
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

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task MigrationExecutor_StagesFullTextOutsideTransactionsAndWritesDurableReport()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_mig_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        var reportPath = Path.Combine(Path.GetTempPath(), $"hmailserver-net10-migration-{Guid.NewGuid():N}.json");
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateMigrationBaselineAsync(testConnectionString).ConfigureAwait(false);
            var store = CreateStore(testConnectionString, databaseName);
            var executor = new SqlServerDatabaseMigrationExecutor(store);
            var result = await executor.Execute5708To6000Async(
                FindRepositoryFile("hmailserver", "source", "DBScripts", "Upgrade5708to6000MSSQL.sql"),
                reportPath,
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(SqlServerDatabaseMigrationStatus.Completed, result.Status, result.Error);
            Assert.AreEqual(5708, result.InitialVersion);
            Assert.AreEqual(6000, result.FinalVersion);
            Assert.IsTrue(result.Checkpoints.Any(checkpoint => checkpoint.Kind == "FullText"));
            Assert.AreEqual(6000, await ReadVersionAsync(testConnectionString).ConfigureAwait(false));
            Assert.IsTrue(await FullTextCatalogExistsAsync(testConnectionString).ConfigureAwait(false));

            using var report = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath).ConfigureAwait(false));
            Assert.AreEqual("Completed", report.RootElement.GetProperty("status").GetString());
            Assert.IsTrue(report.RootElement.GetProperty("checkpoints").GetArrayLength() >= 3);
        }
        finally
        {
            File.Delete(reportPath);
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task MigrationExecutor_RollsBackFailedTransactionalSegmentAndReportsFailure()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_migfail_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        var scriptPath = Path.Combine(Path.GetTempPath(), $"hmailserver-net10-migration-fail-{Guid.NewGuid():N}.sql");
        var reportPath = Path.Combine(Path.GetTempPath(), $"hmailserver-net10-migration-fail-{Guid.NewGuid():N}.json");
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateMigrationBaselineAsync(testConnectionString).ConfigureAwait(false);
            await File.WriteAllTextAsync(
                scriptPath,
                "CREATE TABLE dbo.hm_net10_migration_failure_probe (probeid int NOT NULL)\r\n\r\n" +
                "RAISERROR('Injected migration failure.', 16, 1);").ConfigureAwait(false);
            var executor = new SqlServerDatabaseMigrationExecutor(CreateStore(testConnectionString, databaseName));

            var result = await executor.Execute5708To6000Async(
                scriptPath,
                reportPath,
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(SqlServerDatabaseMigrationStatus.FailedAndRolledBack, result.Status);
            Assert.AreEqual(5708, result.FinalVersion);
            Assert.AreEqual(5708, await ReadVersionAsync(testConnectionString).ConfigureAwait(false));
            Assert.IsFalse(await ObjectExistsAsync(testConnectionString, "hm_net10_migration_failure_probe", "U").ConfigureAwait(false));
            Assert.IsTrue(result.Checkpoints.Any(checkpoint => checkpoint.State == "Failed"));
            Assert.IsTrue(File.Exists(reportPath));
        }
        finally
        {
            File.Delete(scriptPath);
            File.Delete(reportPath);
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

    private static SqlServerDatabaseAdministrationStore CreateStore(string connectionString, string databaseName) =>
        new(
            new SqlServerConnectionFactory(connectionString),
            new LegacyDatabaseConfiguration(
                DatabaseType: 2,
                DatabaseExists: true,
                ServerName: string.Empty,
                DatabaseName: databaseName));

    private static async Task CreateMigrationBaselineAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "CREATE TABLE dbo.hm_dbversion ([value] int NOT NULL); " +
            "INSERT INTO dbo.hm_dbversion ([value]) VALUES (5708); " +
            "CREATE TABLE dbo.hm_messages (" +
            "messageid bigint NULL, messagetype int NULL, messagelocked bit NULL, " +
            "messagenexttrytime datetime NULL, messagesize bigint NULL, messagecurnooftries int NULL);",
            connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        Assert.Fail($"Repository file was not found: {string.Join(Path.DirectorySeparatorChar, parts)}");
        return string.Empty;
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

    private static async Task<int> ReadVersionAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT TOP (1) [value] FROM dbo.hm_dbversion;",
            connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private static async Task<bool> ObjectExistsAsync(
        string connectionString,
        string objectName,
        string objectType)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT CASE WHEN OBJECT_ID(@ObjectName, @ObjectType) IS NULL THEN 0 ELSE 1 END;",
            connection);
        command.Parameters.AddWithValue("@ObjectName", $"dbo.{objectName}");
        command.Parameters.AddWithValue("@ObjectType", objectType);
        return Convert.ToBoolean(await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private static async Task<bool> FullTextCatalogExistsAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'hm_message_search_catalog') THEN 1 ELSE 0 END;",
            connection);
        return Convert.ToBoolean(await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
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
