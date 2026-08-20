using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerSettingsAdministrationStoreAntiSpamSpamAssassinIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task UpdateAntiSpamSpamAssassinAsync_PersistsBothLegacyRowsAndFailsClosedWhenMissing()
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

        var server = new SqlConnectionStringBuilder(raw);
        if (!IsApprovedLocalDataSource(server.DataSource) || !string.IsNullOrWhiteSpace(server.AttachDBFilename))
        {
            Assert.Inconclusive("The SQL integration fixture only accepts a local target without AttachDbFilename.");
        }

        var databaseName = $"hmailserver_net10_antispam_sa_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(server.ConnectionString, "master");
        var testConnectionString = WithDatabase(server.ConnectionString, databaseName);
        var databaseCreated = false;

        try
        {
            await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
            databaseCreated = true;
            await CreateSettingsTableAndSeedAsync(testConnectionString).ConfigureAwait(false);

            var store = new SqlServerSettingsAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString));

            Assert.IsTrue(await store.UpdateAntiSpamSpamAssassinEnabledAsync(true, CancellationToken.None));
            Assert.IsTrue(await store.UpdateAntiSpamSpamAssassinScoreAsync(9, CancellationToken.None));
            Assert.IsTrue(await store.UpdateAntiSpamSpamAssassinMergeScoreAsync(true, CancellationToken.None));
            Assert.IsTrue(await store.UpdateAntiSpamSpamAssassinHostAsync("scanner.example.test", CancellationToken.None));
            Assert.IsTrue(await store.UpdateAntiSpamSpamAssassinPortAsync(1783, CancellationToken.None));
            Assert.IsTrue(await store.UpdateAntiSpamMaximumMessageSizeAsync(4096, CancellationToken.None));
            Assert.IsTrue(await store.UpdateAntiSpamDkimVerificationEnabledAsync(true, CancellationToken.None));
            Assert.IsTrue(await store.UpdateAntiSpamDkimVerificationFailureScoreAsync(11, CancellationToken.None));
            Assert.IsTrue(await store.UpdateAntiSpamBypassGreylistingOnSpfSuccessAsync(true, CancellationToken.None));
            Assert.IsTrue(await store.UpdateAntiSpamBypassGreylistingOnMailFromMxAsync(false, CancellationToken.None));
            Assert.IsTrue(await store.UpdateAntiSpamCheckHostInHeloAsync(true, CancellationToken.None));
            Assert.IsTrue(await store.UpdateAntiSpamCheckHostInHeloScoreAsync(7, CancellationToken.None));
            CollectionAssert.AreEqual(new[] { 1, 1, 9 }, await ReadValuesAsync(testConnectionString));
            Assert.AreEqual("scanner.example.test", await ReadHostAsync(testConnectionString));
            Assert.AreEqual(1783, await ReadPortAsync(testConnectionString));
            Assert.AreEqual(4096, await ReadMaximumMessageSizeAsync(testConnectionString));
            CollectionAssert.AreEqual(new[] { 1, 11 }, await ReadDkimValuesAsync(testConnectionString));
            CollectionAssert.AreEqual(new[] { 0, 1 }, await ReadBypassGreylistingValuesAsync(testConnectionString));
            CollectionAssert.AreEqual(new[] { 1, 7 }, await ReadCheckHostInHeloValuesAsync(testConnectionString));

            await DeleteRowAsync(testConnectionString, "spamassassinenabled");
            Assert.IsFalse(await store.UpdateAntiSpamSpamAssassinEnabledAsync(false, CancellationToken.None));
            await DeleteRowAsync(testConnectionString, "ASDKIMVerificationEnabled");
            Assert.IsFalse(await store.UpdateAntiSpamDkimVerificationEnabledAsync(false, CancellationToken.None));
            await DeleteRowAsync(testConnectionString, "BypassGreylistingOnSPFSuccess");
            Assert.IsFalse(await store.UpdateAntiSpamBypassGreylistingOnSpfSuccessAsync(false, CancellationToken.None));
            await DeleteRowAsync(testConnectionString, "ascheckhostinhelo");
            Assert.IsFalse(await store.UpdateAntiSpamCheckHostInHeloAsync(false, CancellationToken.None));
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
            VALUES (N'spamassassinenabled', N'', 0), (N'spamassassinscore', N'', 2),
                (N'spamassassinmergescore', N'', 0), (N'spamassassinhost', N'127.0.0.1', 0),
                (N'spamassassinport', N'', 783), (N'antispammaxsize', N'', 2048),
                (N'ASDKIMVerificationEnabled', N'', 0), (N'ASDKIMVerificationFailureScore', N'', 4),
                (N'BypassGreylistingOnSPFSuccess', N'', 0), (N'BypassGreylistingOnMailFromMX', N'', 1),
                (N'ascheckhostinhelo', N'', 0), (N'ascheckhostinheloscore', N'', 2);
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
            WHERE settingname IN (N'spamassassinenabled', N'spamassassinscore', N'spamassassinmergescore')
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

    private static async Task<string> ReadHostAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT settingstring FROM dbo.hm_settings WHERE settingname = N'spamassassinhost';",
            connection);
        return Convert.ToString(await command.ExecuteScalarAsync().ConfigureAwait(false)) ?? string.Empty;
    }

    private static async Task<int> ReadPortAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT settinginteger FROM dbo.hm_settings WHERE settingname = N'spamassassinport';",
            connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false));
    }

    private static async Task<int> ReadMaximumMessageSizeAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT settinginteger FROM dbo.hm_settings WHERE settingname = N'antispammaxsize';",
            connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false));
    }

    private static async Task<int[]> ReadDkimValuesAsync(string connectionString)
    {
        const string sql = """
            SELECT settinginteger
            FROM dbo.hm_settings
            WHERE settingname IN (N'ASDKIMVerificationEnabled', N'ASDKIMVerificationFailureScore')
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

    private static async Task<int[]> ReadBypassGreylistingValuesAsync(string connectionString)
    {
        const string sql = """
            SELECT settinginteger
            FROM dbo.hm_settings
            WHERE settingname IN (N'BypassGreylistingOnSPFSuccess', N'BypassGreylistingOnMailFromMX')
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

    private static async Task<int[]> ReadCheckHostInHeloValuesAsync(string connectionString)
    {
        const string sql = """
            SELECT settinginteger
            FROM dbo.hm_settings
            WHERE settingname IN (N'ascheckhostinhelo', N'ascheckhostinheloscore')
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
