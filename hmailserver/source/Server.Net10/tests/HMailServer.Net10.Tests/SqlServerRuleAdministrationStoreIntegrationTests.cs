using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerRuleAdministrationStoreIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task StoreMutations_ExhibitLegacyIdentityReadbackOwnerScopingAndStatementRollback()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_rule_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var store = new SqlServerRuleAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString));

            var insertedId = await store.InsertRuleAsync(
                100,
                new RuleAdministrationSnapshot(
                    Id: 0,
                    AccountId: 100,
                    Name: "New rule",
                    Active: false,
                    UseAnd: false,
                    SortOrder: 1),
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(insertedId > 0);

            var readBack = await store.GetRulesAsync(100, CancellationToken.None).ConfigureAwait(false);
            var inserted = readBack.Single(rule => rule.Id == insertedId);
            Assert.AreEqual(100, inserted.AccountId);
            Assert.AreEqual("New rule", inserted.Name);
            Assert.IsFalse(inserted.Active);
            Assert.IsFalse(inserted.UseAnd);
            Assert.AreEqual(1, inserted.SortOrder);

            var secondId = await store.InsertRuleAsync(
                100,
                new RuleAdministrationSnapshot(
                    Id: 0,
                    AccountId: 100,
                    Name: "Second rule",
                    Active: true,
                    UseAnd: true,
                    SortOrder: 2),
                CancellationToken.None).ConfigureAwait(false);
            Assert.AreNotEqual(insertedId, secondId);
            Assert.AreEqual(3, (await store.GetRulesAsync(100, CancellationToken.None).ConfigureAwait(false)).Count);

            var wrongOwnerUpdate = await store.UpdateRuleAsync(
                999,
                new RuleAdministrationSnapshot(
                    Id: insertedId,
                    AccountId: 999,
                    Name: "changed",
                    Active: true,
                    UseAnd: true,
                    SortOrder: 1),
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(wrongOwnerUpdate);
            Assert.AreEqual(
                "New rule",
                (await store.GetRulesAsync(100, CancellationToken.None).ConfigureAwait(false))
                    .Single(rule => rule.Id == insertedId).Name);

            var ownUpdate = await store.UpdateRuleAsync(
                100,
                new RuleAdministrationSnapshot(
                    Id: insertedId,
                    AccountId: 100,
                    Name: "Renamed rule",
                    Active: true,
                    UseAnd: true,
                    SortOrder: 5),
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(ownUpdate);
            var afterUpdate = (await store.GetRulesAsync(100, CancellationToken.None).ConfigureAwait(false))
                .Single(rule => rule.Id == insertedId);
            Assert.AreEqual("Renamed rule", afterUpdate.Name);
            Assert.IsTrue(afterUpdate.Active);
            Assert.AreEqual(5, afterUpdate.SortOrder);

            var wrongOwnerDelete = await store.DeleteRuleAsync(999, 1, CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(wrongOwnerDelete);
            Assert.AreEqual(3, (await store.GetRulesAsync(100, CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(1, await CountRuleChildRowsAsync(testConnectionString, "hm_rule_criterias", "criteriaruleid", 1).ConfigureAwait(false));
            Assert.AreEqual(1, await CountRuleChildRowsAsync(testConnectionString, "hm_rule_actions", "actionruleid", 1).ConfigureAwait(false));

            var ownDelete = await store.DeleteRuleAsync(100, 1, CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(ownDelete);
            Assert.AreEqual(2, (await store.GetRulesAsync(100, CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(0, await CountRuleChildRowsAsync(testConnectionString, "hm_rule_criterias", "criteriaruleid", 1).ConfigureAwait(false));
            Assert.AreEqual(0, await CountRuleChildRowsAsync(testConnectionString, "hm_rule_actions", "actionruleid", 1).ConfigureAwait(false));

            await Assert.ThrowsExactlyAsync<SqlException>(
                () => store.InsertRuleAsync(
                    100,
                    new RuleAdministrationSnapshot(
                        Id: 0,
                        AccountId: 100,
                        Name: null!,
                        Active: true,
                        UseAnd: true,
                        SortOrder: 0),
                    CancellationToken.None).AsTask()).ConfigureAwait(false);
            Assert.AreEqual(2, (await store.GetRulesAsync(100, CancellationToken.None).ConfigureAwait(false)).Count);

            await Assert.ThrowsExactlyAsync<SqlException>(
                () => store.UpdateRuleAsync(
                    100,
                    new RuleAdministrationSnapshot(
                        Id: secondId,
                        AccountId: 100,
                        Name: null!,
                        Active: true,
                        UseAnd: true,
                        SortOrder: 2),
                    CancellationToken.None).AsTask()).ConfigureAwait(false);
            var afterFailedUpdate = (await store.GetRulesAsync(100, CancellationToken.None).ConfigureAwait(false))
                .Single(rule => rule.Id == secondId);
            Assert.AreEqual("Second rule", afterFailedUpdate.Name);
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
            CREATE TABLE dbo.hm_rules (
                ruleid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                ruleaccountid int NOT NULL,
                rulename nvarchar(100) NOT NULL,
                ruleactive tinyint NOT NULL,
                ruleuseand tinyint NOT NULL,
                rulesortorder int NOT NULL
            );
            CREATE TABLE dbo.hm_rule_criterias (
                criteriaid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                criteriaruleid int NOT NULL,
                criteriausepredefined tinyint NOT NULL,
                criteriapredefinedfield tinyint NOT NULL,
                criteriaheadername nvarchar(255) NOT NULL,
                criteriamatchtype tinyint NOT NULL,
                criteriamatchvalue nvarchar(255) NOT NULL
            );
            CREATE TABLE dbo.hm_rule_actions (
                actionid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                actionruleid int NOT NULL,
                actiontype tinyint NOT NULL,
                actionimapfolder nvarchar(255) NOT NULL,
                actionsubject nvarchar(255) NOT NULL,
                actionfromname nvarchar(255) NOT NULL,
                actionfromaddress nvarchar(255) NOT NULL,
                actionto nvarchar(255) NOT NULL,
                actionbody ntext NOT NULL,
                actionfilename nvarchar(255) NOT NULL,
                actionsortorder int NOT NULL
            );
            INSERT INTO dbo.hm_rules
                (ruleaccountid, rulename, ruleactive, ruleuseand, rulesortorder)
            VALUES
                (100, N'Seeded rule', 1, 1, 0);
            INSERT INTO dbo.hm_rule_criterias
                (criteriaruleid, criteriausepredefined, criteriapredefinedfield, criteriaheadername,
                 criteriamatchtype, criteriamatchvalue)
            VALUES
                (1, 1, 1, N'', 1, N'value');
            INSERT INTO dbo.hm_rule_actions
                (actionruleid, actiontype, actionimapfolder, actionsubject, actionfromname,
                 actionfromaddress, actionto, actionbody, actionfilename, actionsortorder)
            VALUES
                (1, 1, N'', N'', N'', N'', N'', N'', N'', 0);
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<long> CountRuleChildRowsAsync(
        string connectionString,
        string tableName,
        string columnName,
        int ruleId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            $"SELECT COUNT_BIG(*) FROM dbo.{tableName} WHERE {columnName} = @RuleId;",
            connection);
        command.Parameters.Add("@RuleId", SqlDbType.Int).Value = ruleId;
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