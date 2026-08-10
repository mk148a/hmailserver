using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerFetchAccountAdministrationStoreIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreInsert_PreservesLegacyCiphertextAndUidRows()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_fetch_restore_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAsync(testConnectionString).ConfigureAwait(false);
            var store = new SqlServerFetchAccountAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString));
            var encryptedPassword = LegacyBlowfishPasswordCipher.Encrypt("fetch-secret");

            var fetchAccountId = await store.InsertFetchAccountForRestoreAsync(
                new FetchAccountAdministrationDraft(
                    AccountId: 42,
                    Name: "fetcher",
                    ServerAddress: "pop3.example.test",
                    Port: 995,
                    ServerType: 0,
                    Username: "remote-user",
                    MinutesBetweenFetch: 15,
                    DaysToKeepMessages: 30,
                    Enabled: true,
                    ConnectionSecurity: 1),
                encryptedPassword,
                CancellationToken.None).ConfigureAwait(false);
            await store.InsertFetchAccountUidAsync(
                fetchAccountId,
                "uid-1",
                "2026-07-01 12:30:00",
                CancellationToken.None).ConfigureAwait(false);

            var readBack = await store.GetFetchAccountsAsync(42, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(1, readBack.Count);
            Assert.AreEqual(fetchAccountId, readBack[0].Id);
            Assert.AreEqual("fetcher", readBack[0].Name);
            Assert.AreEqual(995, readBack[0].Port);
            Assert.AreEqual(1, readBack[0].ConnectionSecurity);
            Assert.AreEqual(encryptedPassword, await ReadPasswordAsync(testConnectionString, fetchAccountId).ConfigureAwait(false));

            Assert.AreEqual(1, await CountRowsAsync(testConnectionString, "hm_fetchaccounts_uids", "uidfaid", fetchAccountId).ConfigureAwait(false));
            Assert.AreEqual("uid-1", await ReadUidAsync(testConnectionString, fetchAccountId).ConfigureAwait(false));

            await Assert.ThrowsExactlyAsync<InvalidDataException>(
                () => store.InsertFetchAccountForRestoreAsync(
                    new FetchAccountAdministrationDraft(AccountId: 42, Name: "invalid"),
                    "not-legacy-ciphertext",
                    CancellationToken.None).AsTask()).ConfigureAwait(false);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task TransactionScopedRestore_RollsBackFetchAccountAndUidRowsTogether()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_fetch_tx_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAsync(testConnectionString).ConfigureAwait(false);
            var factory = new SqlServerBackupRestoreMetadataTransactionFactory(
                new SqlServerConnectionFactory(testConnectionString));
            var encryptedPassword = LegacyBlowfishPasswordCipher.Encrypt("fetch-secret");

            await using (var transaction = await factory.BeginAsync(CancellationToken.None).ConfigureAwait(false))
            {
                var fetchAccountId = await transaction.FetchAccountStore!
                    .InsertFetchAccountForRestoreAsync(
                        new FetchAccountAdministrationDraft(AccountId: 42, Name: "fetcher"),
                        encryptedPassword,
                        CancellationToken.None).ConfigureAwait(false);
                await transaction.FetchAccountStore.InsertFetchAccountUidAsync(
                    fetchAccountId,
                    "uid-rollback",
                    "2026-07-02 12:30:00",
                    CancellationToken.None).ConfigureAwait(false);
            }

            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_fetchaccounts", "faaccountid", 42).ConfigureAwait(false));
            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_fetchaccounts_uids", "uidfaid", 1).ConfigureAwait(false));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    private static string GetApprovedConnectionStringOrInconclusive()
    {
        var value = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            Assert.Inconclusive($"Set {ConnectionEnvironmentVariable} to an approved disposable SQL Server or LocalDB connection.");
        }

        var builder = new SqlConnectionStringBuilder(value);
        var dataSource = builder.DataSource.Trim();
        var approved = dataSource.Equals(".", StringComparison.OrdinalIgnoreCase)
            || dataSource.Equals("(local)", StringComparison.OrdinalIgnoreCase)
            || dataSource.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || dataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase);
        if (!approved)
        {
            Assert.Inconclusive("The SQL integration source is not an approved local disposable source.");
        }

        if (!string.Equals(
                Environment.GetEnvironmentVariable(AllowDatabaseCreateEnvironmentVariable),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Inconclusive($"Set {AllowDatabaseCreateEnvironmentVariable}=1 to authorize isolated database creation.");
        }

        return builder.ConnectionString;
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
        const string sql = """
            CREATE TABLE dbo.hm_fetchaccounts (
                faid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                faaccountid int NOT NULL,
                faaccountname nvarchar(255) NOT NULL,
                faserveraddress nvarchar(255) NOT NULL,
                faserverport int NOT NULL,
                faservertype tinyint NOT NULL,
                fausername nvarchar(255) NOT NULL,
                fapassword nvarchar(255) NOT NULL,
                faminutes int NOT NULL,
                fanexttry datetime NOT NULL,
                fadaystokeep int NOT NULL,
                faactive tinyint NOT NULL,
                falocked tinyint NOT NULL,
                faprocessmimerecipients tinyint NOT NULL,
                faprocessmimedate tinyint NOT NULL,
                faconnectionsecurity tinyint NOT NULL,
                fauseantispam tinyint NOT NULL,
                fauseantivirus tinyint NOT NULL,
                faenablerouterecipients tinyint NOT NULL,
                famimerecipientheaders nvarchar(255) NOT NULL
            );
            CREATE TABLE dbo.hm_fetchaccounts_uids (
                uidid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                uidfaid int NOT NULL,
                uidvalue nvarchar(255) NOT NULL,
                uidtime datetime NOT NULL
            );
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<string> ReadPasswordAsync(string connectionString, int fetchAccountId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT fapassword FROM dbo.hm_fetchaccounts WHERE faid = @FetchAccountID;",
            connection);
        command.Parameters.Add("@FetchAccountID", SqlDbType.Int).Value = fetchAccountId;
        return Convert.ToString(await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task<string> ReadUidAsync(string connectionString, int fetchAccountId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT uidvalue FROM dbo.hm_fetchaccounts_uids WHERE uidfaid = @FetchAccountID;",
            connection);
        command.Parameters.Add("@FetchAccountID", SqlDbType.Int).Value = fetchAccountId;
        return Convert.ToString(await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task<long> CountRowsAsync(string connectionString, string tableName, string columnName, int value)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            $"SELECT COUNT_BIG(*) FROM dbo.{tableName} WHERE {columnName} = @Value;",
            connection);
        command.Parameters.Add("@Value", SqlDbType.Int).Value = value;
        return Convert.ToInt64(await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
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
