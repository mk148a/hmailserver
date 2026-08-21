using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerSettingsAdministrationStoreImapHierarchyDelimiterIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task UpdateImapHierarchyDelimiterAsync_ReplacesRuleActionPathsAndPersistsSettingAtomically()
    {
        await WithDisposableDatabaseAsync(
            "imapdelim_success",
            async connectionString =>
            {
                await CreateSchemaAndSeedAsync(
                    connectionString,
                    settingValues: new[] { "." },
                    folderNames: new[] { "Parent.Child" },
                    actionFolders: new[] { "Parent.Child" }).ConfigureAwait(false);
                var store = new SqlServerSettingsAdministrationStore(
                    new SqlServerConnectionFactory(connectionString));

                Assert.IsTrue(
                    await store.UpdateImapHierarchyDelimiterAsync("/", CancellationToken.None).ConfigureAwait(false));

                Assert.AreEqual("/", await ReadSettingAsync(connectionString).ConfigureAwait(false));
                Assert.AreEqual("Parent/Child", await ReadActionFolderAsync(connectionString).ConfigureAwait(false));
            }).ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task UpdateImapHierarchyDelimiterAsync_RejectsFolderAndRuleActionConflictsWithoutMutation()
    {
        await WithDisposableDatabaseAsync(
            "imapdelim_conflict",
            async connectionString =>
            {
                await CreateSchemaAndSeedAsync(
                    connectionString,
                    settingValues: new[] { "." },
                    folderNames: new[] { "Parent/Child" },
                    actionFolders: new[] { "Parent/Child" }).ConfigureAwait(false);
                var store = new SqlServerSettingsAdministrationStore(
                    new SqlServerConnectionFactory(connectionString));

                Assert.IsFalse(
                    await store.UpdateImapHierarchyDelimiterAsync("/", CancellationToken.None).ConfigureAwait(false));

                Assert.AreEqual(".", await ReadSettingAsync(connectionString).ConfigureAwait(false));
                Assert.AreEqual("Parent/Child", await ReadActionFolderAsync(connectionString).ConfigureAwait(false));
            }).ConfigureAwait(false);

        await WithDisposableDatabaseAsync(
            "imapdelim_action_conflict",
            async connectionString =>
            {
                await CreateSchemaAndSeedAsync(
                    connectionString,
                    settingValues: new[] { "." },
                    folderNames: new[] { "Parent.Child" },
                    actionFolders: new[] { "Parent/Child" }).ConfigureAwait(false);
                var store = new SqlServerSettingsAdministrationStore(
                    new SqlServerConnectionFactory(connectionString));

                Assert.IsFalse(
                    await store.UpdateImapHierarchyDelimiterAsync("/", CancellationToken.None).ConfigureAwait(false));

                Assert.AreEqual(".", await ReadSettingAsync(connectionString).ConfigureAwait(false));
                Assert.AreEqual("Parent/Child", await ReadActionFolderAsync(connectionString).ConfigureAwait(false));
            }).ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task UpdateImapHierarchyDelimiterAsync_RollsBackRuleActionReplacementWhenSettingRowIsNotSingular()
    {
        await WithDisposableDatabaseAsync(
            "imapdelim_rollback",
            async connectionString =>
            {
                await CreateSchemaAndSeedAsync(
                    connectionString,
                    settingValues: new[] { ".", "." },
                    folderNames: new[] { "Parent.Child" },
                    actionFolders: new[] { "Parent.Child" }).ConfigureAwait(false);
                var store = new SqlServerSettingsAdministrationStore(
                    new SqlServerConnectionFactory(connectionString));

                Assert.IsFalse(
                    await store.UpdateImapHierarchyDelimiterAsync("/", CancellationToken.None).ConfigureAwait(false));

                CollectionAssert.AreEqual(
                    new[] { ".", "." },
                    await ReadSettingsAsync(connectionString).ConfigureAwait(false));
                Assert.AreEqual("Parent.Child", await ReadActionFolderAsync(connectionString).ConfigureAwait(false));
            }).ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task UpdateImapHierarchyDelimiterAsync_SameValueIsNoOpBeforeConflictChecks()
    {
        await WithDisposableDatabaseAsync(
            "imapdelim_noop",
            async connectionString =>
            {
                await CreateSchemaAndSeedAsync(
                    connectionString,
                    settingValues: new[] { "." },
                    folderNames: new[] { "Parent.Child" },
                    actionFolders: new[] { "Parent.Child" }).ConfigureAwait(false);
                var store = new SqlServerSettingsAdministrationStore(
                    new SqlServerConnectionFactory(connectionString));

                Assert.IsTrue(
                    await store.UpdateImapHierarchyDelimiterAsync(".", CancellationToken.None).ConfigureAwait(false));

                Assert.AreEqual(".", await ReadSettingAsync(connectionString).ConfigureAwait(false));
                Assert.AreEqual("Parent.Child", await ReadActionFolderAsync(connectionString).ConfigureAwait(false));
            }).ConfigureAwait(false);
    }

    private static async Task WithDisposableDatabaseAsync(
        string namePrefix,
        Func<string, Task> action)
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_{namePrefix}_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await action(testConnectionString).ConfigureAwait(false);
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

    private static async Task CreateSchemaAndSeedAsync(
        string connectionString,
        IReadOnlyList<string> settingValues,
        IReadOnlyList<string> folderNames,
        IReadOnlyList<string> actionFolders)
    {
        const string schema = """
            CREATE TABLE dbo.hm_settings (
                settingid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                settingname nvarchar(30) NOT NULL,
                settingstring nvarchar(4000) NOT NULL,
                settinginteger int NOT NULL
            );
            CREATE TABLE dbo.hm_imapfolders (foldername nvarchar(255) NOT NULL);
            CREATE TABLE dbo.hm_rule_actions (actionimapfolder nvarchar(255) NOT NULL);
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using (var schemaCommand = new SqlCommand(schema, connection))
        {
            await schemaCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        foreach (var settingValue in settingValues)
        {
            await using var settingCommand = new SqlCommand(
                "INSERT INTO dbo.hm_settings (settingname, settingstring, settinginteger) VALUES (N'IMAPHierarchyDelimiter', @Value, 0);",
                connection);
            settingCommand.Parameters.Add("@Value", System.Data.SqlDbType.NVarChar, 4000).Value = settingValue;
            await settingCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        foreach (var folderName in folderNames)
        {
            await using var folderCommand = new SqlCommand(
                "INSERT INTO dbo.hm_imapfolders (foldername) VALUES (@Value);",
                connection);
            folderCommand.Parameters.Add("@Value", System.Data.SqlDbType.NVarChar, 255).Value = folderName;
            await folderCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        foreach (var actionFolder in actionFolders)
        {
            await using var actionCommand = new SqlCommand(
                "INSERT INTO dbo.hm_rule_actions (actionimapfolder) VALUES (@Value);",
                connection);
            actionCommand.Parameters.Add("@Value", System.Data.SqlDbType.NVarChar, 255).Value = actionFolder;
            await actionCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    private static async Task<string> ReadSettingAsync(string connectionString)
    {
        var values = await ReadSettingsAsync(connectionString).ConfigureAwait(false);
        return values[0];
    }

    private static async Task<string[]> ReadSettingsAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT settingstring FROM dbo.hm_settings WHERE settingname = N'IMAPHierarchyDelimiter' ORDER BY settingid;",
            connection);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var values = new List<string>();
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            values.Add(reader.GetString(0));
        }

        return values.ToArray();
    }

    private static async Task<string> ReadActionFolderAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT actionimapfolder FROM dbo.hm_rule_actions;",
            connection);
        return Convert.ToString(await command.ExecuteScalarAsync().ConfigureAwait(false)) ?? string.Empty;
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
