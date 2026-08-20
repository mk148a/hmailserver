using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerSettingsAdministrationStoreAntiSpamSpfIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task UpdateAntiSpamUseSpfAsync_PersistsBothLegacyRowsAndFailsClosedWhenMissing()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_antispamspf_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        var databaseCreated = false;

        try
        {
            await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
            databaseCreated = true;
            await CreateSettingsTableAndSeedAsync(testConnectionString).ConfigureAwait(false);

            var store = new SqlServerSettingsAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString));

            Assert.IsTrue(await store.UpdateAntiSpamUseSpfAsync(true, CancellationToken.None));
            Assert.IsTrue(await store.UpdateAntiSpamUseSpfScoreAsync(7, CancellationToken.None));
            CollectionAssert.AreEqual(
                new[] { 1, 7 },
                await ReadValuesAsync(testConnectionString));

            await DeleteRowAsync(testConnectionString, "usespf");
            Assert.IsFalse(await store.UpdateAntiSpamUseSpfAsync(false, CancellationToken.None));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            if (databaseCreated)
            {
                await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
            }
        }
    }

    private static string GetApprovedConnectionStringOrInconclusive()
    {
        var raw = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(raw)
            || !string.Equals(
                Environment.GetEnvironmentVariable(AllowDatabaseCreateEnvironmentVariable),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Inconclusive(
                $"Set {ConnectionEnvironmentVariable} to a disposable local SQL target and " +
                $"{AllowDatabaseCreateEnvironmentVariable}=1 to run this destructive fixture.");
        }

        var builder = new SqlConnectionStringBuilder(raw);
        if (!IsApprovedLocalDataSource(builder.DataSource) || !string.IsNullOrWhiteSpace(builder.AttachDBFilename))
        {
            Assert.Inconclusive("The SQL integration fixture only accepts a local target without AttachDbFilename.");
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

    private static async Task CreateSettingsTableAndSeedAsync(string connectionString)
    {
        const string sql = """
            CREATE TABLE dbo.hm_settings (
                settingid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                settingname nvarchar(30) NOT NULL,
                settingstring nvarchar(4000) NOT NULL,
                settinginteger int NOT NULL
            );
            INSERT INTO dbo.hm_settings (settingname, settingstring, settinginteger)
            VALUES (N'usespf', N'', 0), (N'usespfscore', N'', 2);
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<int[]> ReadValuesAsync(string connectionString)
    {
        const string sql = """
            SELECT settinginteger
            FROM dbo.hm_settings
            WHERE settingname IN (N'usespf', N'usespfscore')
            ORDER BY settingname;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var values = new List<int>();
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            values.Add(reader.GetInt32(0));
        }

        return values.ToArray();
    }

    private static async Task DeleteRowAsync(string connectionString, string settingName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "DELETE FROM dbo.hm_settings WHERE settingname = @SettingName;",
            connection);
        command.Parameters.AddWithValue("@SettingName", settingName);
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
