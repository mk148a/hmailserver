using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerImapFolderAdministrationDeletionStoreIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable =
        "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task DeleteFolderAsync_UsesLegacyMessageAndPublicAclRulesAndPreservesInbox()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();

        var databaseName = $"hmailserver_net10_imap_delete_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);

            var store = new SqlServerImapFolderAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString));

            // The current store exposes no fault-injection seam for forcing a statement failure.
            // Residual: rollback behavior is not exercised by this fixture.
            var wrongOwner = new ImapFolderAdministrationSnapshot(
                Id: 100,
                AccountId: 20,
                ParentId: -1,
                Name: "Inbox",
                Subscribed: true,
                CurrentUid: 42,
                CreationTime: "2026-08-01 00:00:00");
            var wrongOwnerResult = await store
                .DeleteFolderAsync(wrongOwner, CancellationToken.None)
                .ConfigureAwait(false);

            Assert.IsFalse(wrongOwnerResult.Succeeded);
            Assert.IsEmpty(wrongOwnerResult.DeletedMessages);
            Assert.AreEqual(3, (await store.GetFoldersForAccountAsync(10, CancellationToken.None)
                .ConfigureAwait(false)).Count);
            Assert.AreEqual(2L, await CountRowsByIdsAsync(
                testConnectionString,
                "hm_messages",
                "messageid",
                new long[] { 1001, 1002 }).ConfigureAwait(false));

            var inbox = new ImapFolderAdministrationSnapshot(
                Id: 100,
                AccountId: 10,
                ParentId: -1,
                Name: "Inbox",
                Subscribed: true,
                CurrentUid: 42,
                CreationTime: "2026-08-01 00:00:00");
            var result = await store
                .DeleteFolderAsync(inbox, CancellationToken.None)
                .ConfigureAwait(false);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(2, result.DeletedMessages.Count);
            Assert.AreEqual(
                new ImapFolderAdministrationDeletedMessage(
                    "child-owned.eml",
                    10,
                    200,
                    "owner@example.test",
                    1),
                result.DeletedMessages[0]);
            Assert.AreEqual(
                new ImapFolderAdministrationDeletedMessage(
                    "nested-owned.eml",
                    10,
                    300,
                    "owner@example.test",
                    1),
                result.DeletedMessages[1]);
            Assert.AreEqual(
                new ImapFolderAdministrationDeletedMessage(
                    "delivered-owned.eml",
                    10,
                    200,
                    "owner@example.test",
                    2),
                result.DeletedMessages[2]);

            var remainingFolders = await store
                .GetFoldersForAccountAsync(10, CancellationToken.None)
                .ConfigureAwait(false);
            Assert.AreEqual(1, remainingFolders.Count);
            Assert.AreEqual(100, remainingFolders[0].Id);
            Assert.AreEqual(-1, remainingFolders[0].ParentId);
            Assert.AreEqual("Inbox", remainingFolders[0].Name);
            Assert.AreEqual(42, remainingFolders[0].CurrentUid);
            Assert.IsEmpty(
                await store.GetChildFoldersAsync(100, 10, CancellationToken.None).ConfigureAwait(false));

            foreach (var table in new[]
                     {
                         (Name: "hm_messages", Column: "messageid"),
                         (Name: "hm_message_search_queue", Column: "messageid"),
                         (Name: "hm_message_search_documents", Column: "messageid")
                     })
            {
                Assert.AreEqual(
                    0L,
                    await CountRowsByIdsAsync(
                        testConnectionString,
                        table.Name,
                        table.Column,
                        new long[] { 1001, 1002, 1003 }).ConfigureAwait(false),
                    $"Owned rows remain in {table.Name}.");
                Assert.AreEqual(
                    2L,
                    await CountRowsByIdsAsync(
                        testConnectionString,
                        table.Name,
                        table.Column,
                        new long[] { 2001, 2002 }).ConfigureAwait(false),
                    $"Cross-account rows were unexpectedly removed from {table.Name}.");
            }

            Assert.AreEqual(
                0L,
                await CountRowsByIdsAsync(
                    testConnectionString,
                    "hm_messagerecipients",
                    "recipientmessageid",
                    new long[] { 1001, 1002 }).ConfigureAwait(false));

            Assert.AreEqual(
                2L,
                await CountRowsByIdsAsync(
                    testConnectionString,
                    "hm_message_metadata",
                    "metadata_messageid",
                    new long[] { 1001, 1002 }).ConfigureAwait(false));
            Assert.AreEqual(
                0L,
                await CountRowsByIdsAsync(
                    testConnectionString,
                    "hm_message_metadata",
                    "metadata_messageid",
                    new long[] { 1003 }).ConfigureAwait(false));
            Assert.AreEqual(
                1L,
                await CountRowsByIdsAsync(
                    testConnectionString,
                    "hm_messagerecipients",
                    "recipientmessageid",
                    new long[] { 1003 }).ConfigureAwait(false));

            Assert.AreEqual(
                3L,
                await CountRowsByIdsAsync(
                    testConnectionString,
                    "hm_acl",
                    "aclsharefolderid",
                    new long[] { 100, 200, 300 }).ConfigureAwait(false));
            Assert.AreEqual(
                1L,
                await CountRowsByIdsAsync(
                    testConnectionString,
                    "hm_acl",
                    "aclsharefolderid",
                    new long[] { 400 }).ConfigureAwait(false));
            Assert.AreEqual(
                1,
                (await store.GetFoldersForAccountAsync(20, CancellationToken.None).ConfigureAwait(false)).Count);
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
        if (string.IsNullOrWhiteSpace(rawConnectionString) ||
            !string.Equals(allowDatabaseCreate, "1", StringComparison.Ordinal))
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

        if (!IsApprovedLocalDataSource(builder.DataSource) ||
            !string.IsNullOrWhiteSpace(builder.AttachDBFilename))
        {
            Assert.Inconclusive(
                "The SQL integration fixture only accepts a local SQL/LocalDB target without AttachDbFilename.");
        }

        return builder.ConnectionString;
    }

    private static bool IsApprovedLocalDataSource(string dataSource)
    {
        var normalized = dataSource.Trim();
        return normalized.Equals(".", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("(local)", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("::1", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(".\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("localhost\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("127.0.0.1,", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("localhost,", StringComparison.OrdinalIgnoreCase);
    }

    private static string WithDatabase(string connectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName
        };
        return builder.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand($"CREATE DATABASE [{databaseName}];", connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task CreateSchemaAndSeedAsync(string connectionString)
    {
        const string sql = """
CREATE TABLE dbo.hm_accounts
(
    accountid int NOT NULL PRIMARY KEY,
    accountaddress nvarchar(255) NOT NULL
);

CREATE TABLE dbo.hm_imapfolders
(
    folderid int NOT NULL PRIMARY KEY,
    folderaccountid int NOT NULL,
    folderparentid int NOT NULL,
    foldername nvarchar(255) NOT NULL,
    folderissubscribed tinyint NOT NULL,
    foldercreationtime datetime NOT NULL,
    foldercurrentuid bigint NOT NULL
);

CREATE TABLE dbo.hm_messages
(
    messageid bigint NOT NULL PRIMARY KEY,
    messageaccountid int NOT NULL,
    messagefolderid int NOT NULL,
    messagefilename nvarchar(255) NOT NULL,
    messagetype tinyint NOT NULL
);

CREATE TABLE dbo.hm_messagerecipients
(
    recipientid bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    recipientmessageid bigint NOT NULL
);

CREATE TABLE dbo.hm_message_metadata
(
    metadata_id bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    metadata_messageid bigint NOT NULL
);

CREATE TABLE dbo.hm_acl
(
    aclid bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    aclsharefolderid bigint NOT NULL
);

CREATE TABLE dbo.hm_message_search_queue
(
    messageid bigint NOT NULL PRIMARY KEY
);

CREATE TABLE dbo.hm_message_search_documents
(
    messageid bigint NOT NULL PRIMARY KEY
);

INSERT INTO dbo.hm_accounts (accountid, accountaddress)
VALUES (10, N'owner@example.test'), (20, N'other@example.test');

INSERT INTO dbo.hm_imapfolders
    (folderid, folderaccountid, folderparentid, foldername, folderissubscribed,
     foldercreationtime, foldercurrentuid)
VALUES
    (100, 10, -1, N'Inbox', 1, CONVERT(datetime, '2026-08-01T00:00:00', 126), 42),
    (200, 10, 100, N'Child', 1, CONVERT(datetime, '2026-08-01T00:01:00', 126), 7),
    (300, 10, 200, N'Nested', 1, CONVERT(datetime, '2026-08-01T00:02:00', 126), 3),
    (400, 20, -1, N'Inbox', 1, CONVERT(datetime, '2026-08-01T00:00:00', 126), 5);

INSERT INTO dbo.hm_messages
    (messageid, messageaccountid, messagefolderid, messagefilename, messagetype)
VALUES
    (1001, 10, 200, N'child-owned.eml', 1),
    (1002, 10, 300, N'nested-owned.eml', 1),
    (1003, 10, 200, N'delivered-owned.eml', 2),
    (2001, 20, 200, N'child-cross-account.eml', 1),
    (2002, 20, 300, N'nested-cross-account.eml', 1);

INSERT INTO dbo.hm_messagerecipients (recipientmessageid)
VALUES (1001), (1002), (1003), (2001), (2002);

INSERT INTO dbo.hm_message_metadata (metadata_messageid)
VALUES (1001), (1002), (1003), (2001), (2002);

INSERT INTO dbo.hm_message_search_queue (messageid)
VALUES (1001), (1002), (1003), (2001), (2002);

INSERT INTO dbo.hm_message_search_documents (messageid)
VALUES (1001), (1002), (1003), (2001), (2002);

INSERT INTO dbo.hm_acl (aclsharefolderid)
VALUES (100), (200), (300), (400);
""";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<long> CountRowsByIdsAsync(
        string connectionString,
        string tableName,
        string columnName,
        IReadOnlyList<long> ids)
    {
        var parameterNames = ids.Select((_, index) => $"@Id{index}").ToArray();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            $"SELECT COUNT_BIG(*) FROM dbo.{tableName} WHERE {columnName} IN ({string.Join(", ", parameterNames)});",
            connection);
        for (var index = 0; index < ids.Count; index++)
        {
            command.Parameters.Add(parameterNames[index], SqlDbType.BigInt).Value = ids[index];
        }

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
