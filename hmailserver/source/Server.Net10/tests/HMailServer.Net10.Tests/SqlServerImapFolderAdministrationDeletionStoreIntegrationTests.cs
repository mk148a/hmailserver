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
            Assert.AreEqual(3, result.DeletedMessages.Count);
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

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreTransaction_PublicFolderCleanupReturnsManifestAndLeavesRollbackToOwner()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();

        var databaseName = $"hmailserver_net10_public_restore_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            await SeedPublicRestoreRowsAsync(testConnectionString).ConfigureAwait(false);

            var transactionFactory = new SqlServerBackupRestoreMetadataTransactionFactory(
                new SqlServerConnectionFactory(testConnectionString));

            await using (var transaction = await transactionFactory
                .BeginAsync(CancellationToken.None)
                .ConfigureAwait(false))
            {
                var manifest = await transaction
                    .DeleteAllPublicFoldersForRestoreWithManifestAsync(CancellationToken.None)
                    .ConfigureAwait(false);

                Assert.AreEqual(2, manifest.Count);
                Assert.AreEqual(
                    new ImapFolderAdministrationDeletedMessage(
                        "public-queued.eml", 0, 500, null, 1),
                    manifest[0]);
                Assert.AreEqual(
                    new ImapFolderAdministrationDeletedMessage(
                        "public-delivered.eml", 0, 500, null, 2),
                    manifest[1]);
            }

            Assert.AreEqual(
                2L,
                await CountRowsByIdsAsync(
                    testConnectionString,
                    "hm_messages",
                    "messageid",
                    new long[] { 5001, 5002 }).ConfigureAwait(false));
            Assert.AreEqual(
                1L,
                await CountRowsByIdsAsync(
                    testConnectionString,
                    "hm_imapfolders",
                    "folderid",
                    new long[] { 500 }).ConfigureAwait(false));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreTransaction_PublicFolderCleanupCommitsManifestAndDependents()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();

        var databaseName = $"hmailserver_net10_public_restore_commit_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            await SeedPublicRestoreRowsAsync(testConnectionString).ConfigureAwait(false);

            var transactionFactory = new SqlServerBackupRestoreMetadataTransactionFactory(
                new SqlServerConnectionFactory(testConnectionString));

            await using (var transaction = await transactionFactory
                .BeginAsync(CancellationToken.None)
                .ConfigureAwait(false))
            {
                var manifest = await transaction
                    .DeleteAllPublicFoldersForRestoreWithManifestAsync(CancellationToken.None)
                    .ConfigureAwait(false);

                Assert.AreEqual(2, manifest.Count);
                await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            Assert.AreEqual(
                0L,
                await CountRowsByIdsAsync(
                    testConnectionString,
                    "hm_messages",
                    "messageid",
                    new long[] { 5001, 5002 }).ConfigureAwait(false));
            Assert.AreEqual(
                0L,
                await CountRowsByIdsAsync(
                    testConnectionString,
                    "hm_imapfolders",
                    "folderid",
                    new long[] { 500 }).ConfigureAwait(false));
            Assert.AreEqual(
                1L,
                await CountRowsByIdsAsync(
                    testConnectionString,
                    "hm_messagerecipients",
                    "recipientmessageid",
                    new long[] { 5001, 5002 }).ConfigureAwait(false));
            Assert.AreEqual(
                0L,
                await CountRowsByIdsAsync(
                    testConnectionString,
                    "hm_message_metadata",
                    "metadata_messageid",
                    new long[] { 5001, 5002 }).ConfigureAwait(false));
            Assert.AreEqual(
                0L,
                await CountRowsByIdsAsync(
                    testConnectionString,
                    "hm_message_search_queue",
                    "messageid",
                    new long[] { 5001, 5002 }).ConfigureAwait(false));
            Assert.AreEqual(
                0L,
                await CountRowsByIdsAsync(
                    testConnectionString,
                    "hm_message_search_documents",
                    "messageid",
                    new long[] { 5001, 5002 }).ConfigureAwait(false));
            Assert.AreEqual(
                0L,
                await CountRowsByIdsAsync(
                    testConnectionString,
                    "hm_acl",
                    "aclsharefolderid",
                    new long[] { 500 }).ConfigureAwait(false));
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
    accountid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    accountdomainid int NOT NULL,
    accountadminlevel tinyint NOT NULL,
    accountaddress nvarchar(255) NOT NULL UNIQUE,
    accountpassword nvarchar(255) NOT NULL,
    accountactive int NOT NULL,
    accountisad int NOT NULL,
    accountaddomain nvarchar(255) NOT NULL,
    accountadusername nvarchar(255) NOT NULL,
    accountmaxsize int NOT NULL,
    accountvacationmessageon tinyint NOT NULL,
    accountvacationmessage nvarchar(1000) NOT NULL,
    accountvacationsubject nvarchar(200) NOT NULL,
    accountpwencryption tinyint NOT NULL,
    accountforwardenabled tinyint NOT NULL,
    accountforwardaddress nvarchar(255) NOT NULL,
    accountforwardkeeporiginal tinyint NOT NULL,
    accountenablesignature tinyint NOT NULL,
    accountsignatureplaintext nvarchar(max) NOT NULL,
    accountsignaturehtml nvarchar(max) NOT NULL,
    accountlastlogontime datetime NOT NULL,
    accountvacationexpires tinyint NOT NULL,
    accountvacationexpiredate datetime NOT NULL,
    accountpersonfirstname nvarchar(60) NOT NULL,
    accountpersonlastname nvarchar(60) NOT NULL,
    accountvacationabortspamflagged tinyint NOT NULL,
    accountforwardabortspamflagged tinyint NOT NULL
);

CREATE TABLE dbo.hm_imapfolders
(
    folderid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    folderaccountid int NOT NULL,
    folderparentid int NOT NULL,
    foldername nvarchar(255) NOT NULL,
    folderissubscribed tinyint NOT NULL,
    foldercreationtime datetime NOT NULL,
    foldercurrentuid bigint NOT NULL,
    CONSTRAINT hm_imapfolders_unique UNIQUE (folderaccountid, folderparentid, foldername)
);
CREATE INDEX idx_hm_imapfolders_folderaccountid ON dbo.hm_imapfolders (folderaccountid);

CREATE TABLE dbo.hm_messages
(
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
    messageuid bigint NOT NULL
);
CREATE INDEX idx_hm_messages ON dbo.hm_messages (messageaccountid, messagefolderid);
CREATE INDEX idx_hm_messages_type ON dbo.hm_messages (messagetype);

CREATE TABLE dbo.hm_messagerecipients
(
    recipientid bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    recipientmessageid bigint NOT NULL,
    recipientaddress nvarchar(255) NOT NULL,
    recipientlocalaccountid int NOT NULL,
    recipientoriginaladdress nvarchar(255) NOT NULL
);
CREATE INDEX idx_hm_messagerecipients_recipientmessageid
    ON dbo.hm_messagerecipients (recipientmessageid);

CREATE TABLE dbo.hm_message_metadata
(
    metadata_id bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    metadata_accountid int NOT NULL,
    metadata_folderid int NOT NULL,
    metadata_messageid bigint NOT NULL,
    metadata_dateutc datetime NULL,
    metadata_from nvarchar(255) NOT NULL,
    metadata_subject nvarchar(255) NOT NULL,
    metadata_to nvarchar(255) NOT NULL,
    metadata_cc nvarchar(255) NOT NULL,
    CONSTRAINT hm_message_metadata_unique UNIQUE (metadata_accountid, metadata_folderid, metadata_messageid)
);
CREATE INDEX idx_message_metadata_id ON dbo.hm_message_metadata (metadata_messageid);

CREATE TABLE dbo.hm_acl
(
    aclid bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    aclsharefolderid bigint NOT NULL,
    aclpermissiontype tinyint NOT NULL,
    aclpermissiongroupid bigint NOT NULL,
    aclpermissionaccountid bigint NOT NULL,
    aclvalue bigint NOT NULL,
    CONSTRAINT hm_acl_unique UNIQUE (aclsharefolderid, aclpermissiontype, aclpermissiongroupid, aclpermissionaccountid)
);

CREATE TABLE dbo.hm_message_search_queue
(
    messageid bigint NOT NULL PRIMARY KEY,
    queuedutc datetime2(3) NOT NULL,
    attempts int NOT NULL,
    lastattemptutc datetime2(3) NULL,
    nextattemptutc datetime2(3) NULL,
    searchleaseowner nvarchar(128) NULL,
    searchleaseexpiresutc datetime2(3) NULL,
    lasterror nvarchar(1024) NULL
);
CREATE INDEX idx_hm_message_search_queue_lease
    ON dbo.hm_message_search_queue (nextattemptutc, searchleaseexpiresutc, attempts, queuedutc);

CREATE TABLE dbo.hm_message_search_documents
(
    messageid bigint NOT NULL PRIMARY KEY,
    messageaccountid int NOT NULL,
    messagefolderid int NOT NULL,
    messageuid bigint NOT NULL,
    messageinternaldateutc datetime2(3) NOT NULL,
    messagesize bigint NOT NULL,
    messageflags tinyint NOT NULL,
    search_header nvarchar(max) NOT NULL,
    search_body nvarchar(max) NOT NULL,
    search_combined nvarchar(max) NOT NULL,
    updatedutc datetime2(3) NOT NULL
);
CREATE INDEX idx_hm_message_search_documents_folder_uid
    ON dbo.hm_message_search_documents (messageaccountid, messagefolderid, messageuid);

SET IDENTITY_INSERT dbo.hm_accounts ON;
INSERT INTO dbo.hm_accounts
    (accountid, accountdomainid, accountadminlevel, accountaddress, accountpassword,
     accountactive, accountisad, accountaddomain, accountadusername, accountmaxsize,
     accountvacationmessageon, accountvacationmessage, accountvacationsubject,
     accountpwencryption, accountforwardenabled, accountforwardaddress,
     accountforwardkeeporiginal, accountenablesignature, accountsignatureplaintext,
     accountsignaturehtml, accountlastlogontime, accountvacationexpires,
     accountvacationexpiredate, accountpersonfirstname, accountpersonlastname,
     accountvacationabortspamflagged, accountforwardabortspamflagged)
VALUES
    (10, 1, 0, N'owner@example.test', N'', 1, 0, N'', N'', 0, 0, N'', N'', 0, 0, N'',
     0, 0, N'', N'', CONVERT(datetime, '2026-08-01T00:00:00', 126), 0,
     CONVERT(datetime, '2026-08-01T00:00:00', 126), N'', N'', 0, 0),
    (20, 1, 0, N'other@example.test', N'', 1, 0, N'', N'', 0, 0, N'', N'', 0, 0, N'',
     0, 0, N'', N'', CONVERT(datetime, '2026-08-01T00:00:00', 126), 0,
     CONVERT(datetime, '2026-08-01T00:00:00', 126), N'', N'', 0, 0);
SET IDENTITY_INSERT dbo.hm_accounts OFF;

SET IDENTITY_INSERT dbo.hm_imapfolders ON;
INSERT INTO dbo.hm_imapfolders
    (folderid, folderaccountid, folderparentid, foldername, folderissubscribed,
     foldercreationtime, foldercurrentuid)
VALUES
    (100, 10, -1, N'Inbox', 1, CONVERT(datetime, '2026-08-01T00:00:00', 126), 42),
    (200, 10, 100, N'Child', 1, CONVERT(datetime, '2026-08-01T00:01:00', 126), 7),
    (300, 10, 200, N'Nested', 1, CONVERT(datetime, '2026-08-01T00:02:00', 126), 3),
    (400, 20, -1, N'Inbox', 1, CONVERT(datetime, '2026-08-01T00:00:00', 126), 5);
SET IDENTITY_INSERT dbo.hm_imapfolders OFF;

SET IDENTITY_INSERT dbo.hm_messages ON;
INSERT INTO dbo.hm_messages
    (messageid, messageaccountid, messagefolderid, messagefilename, messagetype, messagefrom,
     messagesize, messagecurnooftries, messagenexttrytime, messageflags, messagecreatetime,
     messagelocked, messageuid)
VALUES
    (1001, 10, 200, N'child-owned.eml', 1, N'from@example.test', 10, 0, CONVERT(datetime, '2026-08-01T00:00:00', 126), 0, CONVERT(datetime, '2026-08-01T00:00:00', 126), 0, 1),
    (1002, 10, 300, N'nested-owned.eml', 1, N'from@example.test', 20, 0, CONVERT(datetime, '2026-08-01T00:00:00', 126), 0, CONVERT(datetime, '2026-08-01T00:00:00', 126), 0, 2),
    (1003, 10, 200, N'delivered-owned.eml', 2, N'from@example.test', 30, 0, CONVERT(datetime, '2026-08-01T00:00:00', 126), 0, CONVERT(datetime, '2026-08-01T00:00:00', 126), 0, 3),
    (2001, 20, 200, N'child-cross-account.eml', 1, N'from@example.test', 40, 0, CONVERT(datetime, '2026-08-01T00:00:00', 126), 0, CONVERT(datetime, '2026-08-01T00:00:00', 126), 0, 4),
    (2002, 20, 300, N'nested-cross-account.eml', 1, N'from@example.test', 50, 0, CONVERT(datetime, '2026-08-01T00:00:00', 126), 0, CONVERT(datetime, '2026-08-01T00:00:00', 126), 0, 5);
SET IDENTITY_INSERT dbo.hm_messages OFF;

INSERT INTO dbo.hm_messagerecipients
    (recipientmessageid, recipientaddress, recipientlocalaccountid, recipientoriginaladdress)
VALUES
    (1001, N'recipient@example.test', 10, N'recipient@example.test'),
    (1002, N'recipient@example.test', 10, N'recipient@example.test'),
    (1003, N'recipient@example.test', 10, N'recipient@example.test'),
    (2001, N'recipient@example.test', 20, N'recipient@example.test'),
    (2002, N'recipient@example.test', 20, N'recipient@example.test');

INSERT INTO dbo.hm_message_metadata
    (metadata_accountid, metadata_folderid, metadata_messageid, metadata_dateutc,
     metadata_from, metadata_subject, metadata_to, metadata_cc)
VALUES
    (10, 200, 1001, NULL, N'from@example.test', N'subject', N'to@example.test', N''),
    (10, 300, 1002, NULL, N'from@example.test', N'subject', N'to@example.test', N''),
    (10, 200, 1003, NULL, N'from@example.test', N'subject', N'to@example.test', N''),
    (20, 200, 2001, NULL, N'from@example.test', N'subject', N'to@example.test', N''),
    (20, 300, 2002, NULL, N'from@example.test', N'subject', N'to@example.test', N'');

INSERT INTO dbo.hm_message_search_queue
    (messageid, queuedutc, attempts)
VALUES
    (1001, CONVERT(datetime2(3), '2026-08-01T00:00:00', 126), 0),
    (1002, CONVERT(datetime2(3), '2026-08-01T00:00:00', 126), 0),
    (1003, CONVERT(datetime2(3), '2026-08-01T00:00:00', 126), 0),
    (2001, CONVERT(datetime2(3), '2026-08-01T00:00:00', 126), 0),
    (2002, CONVERT(datetime2(3), '2026-08-01T00:00:00', 126), 0);

INSERT INTO dbo.hm_message_search_documents
    (messageid, messageaccountid, messagefolderid, messageuid, messageinternaldateutc,
     messagesize, messageflags, search_header, search_body, search_combined, updatedutc)
VALUES
    (1001, 10, 200, 1, CONVERT(datetime2(3), '2026-08-01T00:00:00', 126), 10, 0, N'', N'', N'', CONVERT(datetime2(3), '2026-08-01T00:00:00', 126)),
    (1002, 10, 300, 2, CONVERT(datetime2(3), '2026-08-01T00:00:00', 126), 20, 0, N'', N'', N'', CONVERT(datetime2(3), '2026-08-01T00:00:00', 126)),
    (1003, 10, 200, 3, CONVERT(datetime2(3), '2026-08-01T00:00:00', 126), 30, 0, N'', N'', N'', CONVERT(datetime2(3), '2026-08-01T00:00:00', 126)),
    (2001, 20, 200, 4, CONVERT(datetime2(3), '2026-08-01T00:00:00', 126), 40, 0, N'', N'', N'', CONVERT(datetime2(3), '2026-08-01T00:00:00', 126)),
    (2002, 20, 300, 5, CONVERT(datetime2(3), '2026-08-01T00:00:00', 126), 50, 0, N'', N'', N'', CONVERT(datetime2(3), '2026-08-01T00:00:00', 126));

INSERT INTO dbo.hm_acl
    (aclsharefolderid, aclpermissiontype, aclpermissiongroupid, aclpermissionaccountid, aclvalue)
VALUES
    (100, 0, 0, 10, 1), (200, 0, 0, 10, 1), (300, 0, 0, 10, 1), (400, 0, 0, 20, 1);
""";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task SeedPublicRestoreRowsAsync(string connectionString)
    {
        const string sql = """
SET IDENTITY_INSERT dbo.hm_imapfolders ON;
INSERT INTO dbo.hm_imapfolders
    (folderid, folderaccountid, folderparentid, foldername, folderissubscribed,
     foldercreationtime, foldercurrentuid)
VALUES
    (500, 0, -1, N'Public', 1, CONVERT(datetime, '2026-08-01T00:00:00', 126), 1);
SET IDENTITY_INSERT dbo.hm_imapfolders OFF;

SET IDENTITY_INSERT dbo.hm_messages ON;
INSERT INTO dbo.hm_messages
    (messageid, messageaccountid, messagefolderid, messagefilename, messagetype,
     messagefrom, messagesize, messagecurnooftries, messagenexttrytime, messageflags,
     messagecreatetime, messagelocked, messageuid)
VALUES
    (5001, 0, 500, N'public-queued.eml', 1, N'from@example.test', 10, 0,
     CONVERT(datetime, '2026-08-01T00:00:00', 126), 0,
     CONVERT(datetime, '2026-08-01T00:00:00', 126), 0, 1),
    (5002, 0, 500, N'public-delivered.eml', 2, N'from@example.test', 20, 0,
     CONVERT(datetime, '2026-08-01T00:00:00', 126), 0,
     CONVERT(datetime, '2026-08-01T00:00:00', 126), 0, 2);
SET IDENTITY_INSERT dbo.hm_messages OFF;

INSERT INTO dbo.hm_messagerecipients
    (recipientmessageid, recipientaddress, recipientlocalaccountid, recipientoriginaladdress)
VALUES
    (5001, N'recipient@example.test', 0, N'recipient@example.test'),
    (5002, N'recipient@example.test', 0, N'recipient@example.test');

INSERT INTO dbo.hm_message_metadata
    (metadata_accountid, metadata_folderid, metadata_messageid, metadata_dateutc,
     metadata_from, metadata_subject, metadata_to, metadata_cc)
VALUES
    (0, 500, 5001, NULL, N'from@example.test', N'subject', N'to@example.test', N''),
    (0, 500, 5002, NULL, N'from@example.test', N'subject', N'to@example.test', N'');

INSERT INTO dbo.hm_message_search_queue
    (messageid, queuedutc, attempts)
VALUES
    (5001, CONVERT(datetime2(3), '2026-08-01T00:00:00', 126), 0),
    (5002, CONVERT(datetime2(3), '2026-08-01T00:00:00', 126), 0);

INSERT INTO dbo.hm_message_search_documents
    (messageid, messageaccountid, messagefolderid, messageuid, messageinternaldateutc,
     messagesize, messageflags, search_header, search_body, search_combined, updatedutc)
VALUES
    (5001, 0, 500, 1, CONVERT(datetime2(3), '2026-08-01T00:00:00', 126), 10, 0, N'', N'', N'', CONVERT(datetime2(3), '2026-08-01T00:00:00', 126)),
    (5002, 0, 500, 2, CONVERT(datetime2(3), '2026-08-01T00:00:00', 126), 20, 0, N'', N'', N'', CONVERT(datetime2(3), '2026-08-01T00:00:00', 126));

INSERT INTO dbo.hm_acl
    (aclsharefolderid, aclpermissiontype, aclpermissiongroupid, aclpermissionaccountid, aclvalue)
VALUES
    (500, 0, 0, 0, 1);
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
