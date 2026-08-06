using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerMessageAdministrationStoreIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task InsertMessage_ExhibitsLegacyIdentityReadbackAndStatementRollback()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_message_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var store = new SqlServerMessageAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString));

            var insertedId = await store.InsertMessageAsync(
                10,
                20,
                new MessageAdministrationSnapshot(
                    Id: 0,
                    AccountId: 10,
                    FolderId: 20,
                    FileName: "00000000-0000-0000-0000-000000000001.eml",
                    State: 2,
                    FromAddress: "sender@example.test",
                    SizeBytes: 2048,
                    CurrentNumberOfTries: 0,
                    Flags: 0,
                    InternalDate: new DateTime(2026, 1, 2, 3, 4, 5),
                    Uid: 1),
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(insertedId > 0);

            var readBack = await store.GetFolderMessagesAsync(20, CancellationToken.None).ConfigureAwait(false);
            var inserted = readBack.Single(message => message.Id == insertedId);
            Assert.AreEqual(10, inserted.AccountId);
            Assert.AreEqual(20, inserted.FolderId);
            Assert.AreEqual("00000000-0000-0000-0000-000000000001.eml", inserted.FileName);
            Assert.AreEqual(2, inserted.State);
            Assert.AreEqual("sender@example.test", inserted.FromAddress);
            Assert.AreEqual(2048, inserted.SizeBytes);
            Assert.AreEqual(1, inserted.Uid);

            var secondId = await store.InsertMessageAsync(
                10,
                20,
                new MessageAdministrationSnapshot(
                    Id: 0,
                    AccountId: 10,
                    FolderId: 20,
                    FileName: "00000000-0000-0000-0000-000000000002.eml",
                    State: 2,
                    FromAddress: "other@example.test",
                    SizeBytes: 512,
                    CurrentNumberOfTries: 0,
                    Flags: 0,
                    InternalDate: new DateTime(2026, 1, 3, 3, 4, 5),
                    Uid: 2),
                CancellationToken.None).ConfigureAwait(false);
            Assert.AreNotEqual(insertedId, secondId);
            Assert.AreEqual(3, (await store.GetFolderMessagesAsync(20, CancellationToken.None).ConfigureAwait(false)).Count);

            await Assert.ThrowsExactlyAsync<SqlException>(
                () => store.InsertMessageAsync(
                    10,
                    20,
                    new MessageAdministrationSnapshot(
                        Id: 0,
                        AccountId: 10,
                        FolderId: 20,
                        FileName: "seed.eml",
                        State: 2,
                        FromAddress: string.Empty,
                        SizeBytes: 0,
                        CurrentNumberOfTries: 0,
                        Flags: 0,
                        InternalDate: new DateTime(2026, 1, 4),
                        Uid: 3),
                    CancellationToken.None).AsTask()).ConfigureAwait(false);
            Assert.AreEqual(3, (await store.GetFolderMessagesAsync(20, CancellationToken.None).ConfigureAwait(false)).Count);
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
            CREATE TABLE dbo.hm_messages (
                messageid bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                messageaccountid int NOT NULL,
                messagefolderid int NOT NULL,
                messagefilename nvarchar(255) NOT NULL,
                messagetype tinyint NOT NULL,
                messagefrom nvarchar(255) NOT NULL,
                messagesize bigint NOT NULL,
                messagecurnooftries int NOT NULL,
                messagenexttrytime datetime NOT NULL,
                messageflags tinyint NOT NULL,
                messagecreatetime datetime NOT NULL,
                messagelocked tinyint NOT NULL,
                messageuid bigint NOT NULL,
                CONSTRAINT u_hm_messages_filename UNIQUE (messagefilename)
            );
            INSERT INTO dbo.hm_messages
                (messageaccountid, messagefolderid, messagefilename, messagetype, messagefrom,
                 messagesize, messagecurnooftries, messagenexttrytime, messageflags,
                 messagecreatetime, messagelocked, messageuid)
            VALUES
                (10, 20, N'seed.eml', 2, N'seed@example.test', 1024, 0, '2026-01-01T00:00:00', 0, '2026-01-01T00:00:00', 0, 1);
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