using System.Data;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerDistributionListAdministrationStoreIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task DeleteDistributionList_UsesOwnerScopeAndRollsBackWhenParentDeleteAffectsZeroRows()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_distlist_delete_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var store = new SqlServerDistributionListAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString));

            Assert.IsTrue(await store.DeleteDistributionListAsync(1, 1, CancellationToken.None).ConfigureAwait(false));
            Assert.AreEqual(0, await CountAsync(testConnectionString, "hm_distributionlists", "distributionlistid", 1).ConfigureAwait(false));
            Assert.AreEqual(0, await CountAsync(testConnectionString, "hm_distributionlistsrecipients", "distributionlistrecipientlistid", 1).ConfigureAwait(false));
            Assert.AreEqual(1, await CountAsync(testConnectionString, "hm_distributionlists", "distributionlistid", 2).ConfigureAwait(false));
            Assert.AreEqual(1, await CountAsync(testConnectionString, "hm_distributionlistsrecipients", "distributionlistrecipientlistid", 2).ConfigureAwait(false));

            Assert.IsFalse(await store.DeleteDistributionListAsync(1, 2, CancellationToken.None).ConfigureAwait(false));
            Assert.AreEqual(1, await CountAsync(testConnectionString, "hm_distributionlists", "distributionlistid", 2).ConfigureAwait(false));
            Assert.AreEqual(1, await CountAsync(testConnectionString, "hm_distributionlistsrecipients", "distributionlistrecipientlistid", 2).ConfigureAwait(false));

            await InstallFailingDeleteTriggerAsync(testConnectionString).ConfigureAwait(false);
            await Assert.ThrowsExactlyAsync<SqlException>(
                () => store.DeleteDistributionListAsync(2, 2, CancellationToken.None).AsTask()).ConfigureAwait(false);
            Assert.AreEqual(1, await CountAsync(testConnectionString, "hm_distributionlists", "distributionlistid", 2).ConfigureAwait(false));
            Assert.AreEqual(1, await CountAsync(testConnectionString, "hm_distributionlistsrecipients", "distributionlistrecipientlistid", 2).ConfigureAwait(false));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    private static string GetApprovedConnectionStringOrInconclusive()
    {
        var raw = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        var allowCreate = Environment.GetEnvironmentVariable(AllowDatabaseCreateEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(raw) || !string.Equals(allowCreate, "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive(
                $"Set {ConnectionEnvironmentVariable} to a disposable local SQL target and " +
                $"{AllowDatabaseCreateEnvironmentVariable}=1 to run this destructive fixture.");
        }

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(raw);
        }
        catch (ArgumentException exception)
        {
            Assert.Inconclusive($"The SQL integration connection string is invalid: {exception.Message}");
            throw;
        }

        if (!IsApprovedLocalDataSource(builder.DataSource) || !string.IsNullOrWhiteSpace(builder.AttachDBFilename))
        {
            Assert.Inconclusive("The SQL integration fixture only accepts a local SQL target without AttachDbFilename.");
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
            || normalized.StartsWith(".\\", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("localhost,", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("127.0.0.1,", StringComparison.OrdinalIgnoreCase);
    }

    private static string WithDatabase(string connectionString, string databaseName) =>
        new SqlConnectionStringBuilder(connectionString) { InitialCatalog = databaseName }.ConnectionString;

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
            CREATE TABLE dbo.hm_distributionlists (
                distributionlistid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                distributionlistdomainid int NOT NULL,
                distributionlistaddress nvarchar(255) NOT NULL
            );
            CREATE TABLE dbo.hm_distributionlistsrecipients (
                distributionlistrecipientid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                distributionlistrecipientlistid int NOT NULL,
                distributionlistrecipientaddress nvarchar(255) NOT NULL
            );
            INSERT INTO dbo.hm_distributionlists (distributionlistdomainid, distributionlistaddress)
            VALUES (1, N'one@example.test'), (2, N'two@example.test');
            INSERT INTO dbo.hm_distributionlistsrecipients (distributionlistrecipientlistid, distributionlistrecipientaddress)
            VALUES (1, N'one-recipient@example.test'), (2, N'two-recipient@example.test');
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task InstallFailingDeleteTriggerAsync(string connectionString)
    {
        const string sql = """
            CREATE TRIGGER dbo.trg_hm_distributionlists_fail_delete
            ON dbo.hm_distributionlists
            AFTER DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51000, 'Intentional disposable parent-delete failure', 1;
            END;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<int> CountAsync(string connectionString, string table, string column, int value)
    {
        var sql = $"SELECT COUNT(*) FROM dbo.{table} WHERE {column} = @Value;";
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Value", SqlDbType.Int).Value = value;
        return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false));
    }

    private static async Task DropDatabaseAsync(string masterConnectionString, string databaseName)
    {
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END;",
            connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
