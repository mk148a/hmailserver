using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerDomainAdministrationStoreIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task StoreMutations_ExhibitLegacyIdentityReadbackOwnerScopingAndStatementRollback()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_domain_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var store = new SqlServerDomainAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString));

            var insertedId = await store.InsertDomainAsync(
                new DomainAdministrationSnapshot(
                    Id: 0,
                    Name: "new.example",
                    Active: true,
                    Postmaster: "postmaster@new.example",
                    MaxMessageSize: 2048,
                    PlusAddressingEnabled: true,
                    PlusAddressingCharacter: "+",
                    AntiSpamEnableGreylisting: true,
                    AdDomainName: string.Empty,
                    MaxSize: 0,
                    Size: 0,
                    AllocatedSize: 0,
                    MaxNumberOfAccounts: 5,
                    MaxNumberOfAliases: 10,
                    MaxNumberOfDistributionLists: 20,
                    MaxNumberOfAccountsEnabled: true,
                    MaxNumberOfAliasesEnabled: true,
                    MaxNumberOfDistributionListsEnabled: false,
                    MaxAccountSize: 100,
                    SignatureEnabled: true,
                    SignatureMethod: 2,
                    SignaturePlainText: "plain",
                    SignatureHtml: "<b>html</b>",
                    AddSignaturesToReplies: true,
                    AddSignaturesToLocalMail: false,
                    DkimSignEnabled: true,
                    DkimSelector: "sel",
                    DkimPrivateKeyFile: "key.pem",
                    DkimHeaderCanonicalizationMethod: 1,
                    DkimBodyCanonicalizationMethod: 2,
                    DkimSigningAlgorithm: 1,
                    DkimSignAliasesEnabled: true),
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(insertedId > 0);

            var readBack = (await store.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false))
                .Single(domain => domain.Id == insertedId);
            Assert.AreEqual("new.example", readBack.Name);
            Assert.IsTrue(readBack.Active);
            Assert.AreEqual("postmaster@new.example", readBack.Postmaster);
            Assert.AreEqual(2048, readBack.MaxMessageSize);
            Assert.IsTrue(readBack.PlusAddressingEnabled);
            Assert.IsTrue(readBack.AntiSpamEnableGreylisting);
            Assert.AreEqual(5, readBack.MaxNumberOfAccounts);
            Assert.IsTrue(readBack.MaxNumberOfAccountsEnabled);
            Assert.IsTrue(readBack.MaxNumberOfAliasesEnabled);
            Assert.IsFalse(readBack.MaxNumberOfDistributionListsEnabled);
            Assert.IsTrue(readBack.SignatureEnabled);
            Assert.AreEqual(2, readBack.SignatureMethod);
            Assert.IsTrue(readBack.DkimSignEnabled);
            Assert.AreEqual(1, readBack.DkimHeaderCanonicalizationMethod);
            Assert.AreEqual(1, readBack.DkimSigningAlgorithm);
            Assert.IsTrue(readBack.DkimSignAliasesEnabled);

            var secondId = await store.InsertDomainAsync(
                new DomainAdministrationSnapshot(Id: 0, Name: "other.example", Active: false),
                CancellationToken.None).ConfigureAwait(false);
            Assert.AreNotEqual(insertedId, secondId);
            Assert.AreEqual(3, (await store.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Count);

            var unknownUpdate = await store.UpdateDomainAsync(
                new DomainAdministrationSnapshot(Id: 9999, Name: "renamed.example", Active: true),
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(unknownUpdate);
            Assert.AreEqual(
                "new.example",
                (await store.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false))
                    .Single(domain => domain.Id == insertedId).Name);

            var ownUpdate = await store.UpdateDomainAsync(
                new DomainAdministrationSnapshot(
                    Id: insertedId,
                    Name: "renamed.example",
                    Active: false,
                    Postmaster: "pm@renamed.example",
                    MaxMessageSize: 4096,
                    PlusAddressingEnabled: false,
                    PlusAddressingCharacter: "+",
                    AntiSpamEnableGreylisting: false,
                    AdDomainName: string.Empty,
                    MaxSize: 0,
                    Size: 0,
                    AllocatedSize: 0,
                    MaxNumberOfAccounts: 3,
                    MaxNumberOfAliases: 0,
                    MaxNumberOfDistributionLists: 0,
                    MaxNumberOfAccountsEnabled: false,
                    MaxNumberOfAliasesEnabled: false,
                    MaxNumberOfDistributionListsEnabled: true,
                    MaxAccountSize: 0,
                    SignatureEnabled: false,
                    SignatureMethod: 1,
                    SignaturePlainText: string.Empty,
                    SignatureHtml: string.Empty,
                    AddSignaturesToReplies: false,
                    AddSignaturesToLocalMail: true,
                    DkimSignEnabled: false,
                    DkimSelector: string.Empty,
                    DkimPrivateKeyFile: string.Empty,
                    DkimHeaderCanonicalizationMethod: 2,
                    DkimBodyCanonicalizationMethod: 2,
                    DkimSigningAlgorithm: 2,
                    DkimSignAliasesEnabled: false),
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(ownUpdate);
            var afterUpdate = (await store.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false))
                .Single(domain => domain.Id == insertedId);
            Assert.AreEqual(insertedId, afterUpdate.Id);
            Assert.AreEqual("renamed.example", afterUpdate.Name);
            Assert.IsFalse(afterUpdate.Active);
            Assert.IsFalse(afterUpdate.AntiSpamEnableGreylisting);
            Assert.IsTrue(afterUpdate.MaxNumberOfDistributionListsEnabled);
            Assert.IsFalse(afterUpdate.DkimSignEnabled);

            var unknownDelete = await store.DeleteDomainByIdAsync(9999, CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(unknownDelete);
            Assert.AreEqual(3, (await store.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(1, await CountRowsAsync(testConnectionString, "hm_accounts", "accountdomainid", 1).ConfigureAwait(false));

            var cascadeDelete = await store.DeleteDomainByIdAsync(1, CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(cascadeDelete);
            Assert.AreEqual(2, (await store.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_accounts", "accountdomainid", 1).ConfigureAwait(false));
            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_aliases", "aliasdomainid", 1).ConfigureAwait(false));
            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_distributionlists", "distributionlistdomainid", 1).ConfigureAwait(false));
            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_distributionlistsrecipients", "distributionlistrecipientlistid", 1).ConfigureAwait(false));
            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_domain_aliases", "dadomainid", 1).ConfigureAwait(false));
            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_rules", "ruleaccountid", 1).ConfigureAwait(false));
            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_messages", "messageaccountid", 1).ConfigureAwait(false));

            await Assert.ThrowsExactlyAsync<SqlException>(
                () => store.InsertDomainAsync(
                    new DomainAdministrationSnapshot(Id: 0, Name: null!, Active: true),
                    CancellationToken.None).AsTask()).ConfigureAwait(false);
            Assert.AreEqual(2, (await store.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false)).Count);

            await Assert.ThrowsExactlyAsync<SqlException>(
                () => store.UpdateDomainAsync(
                    new DomainAdministrationSnapshot(Id: secondId, Name: null!, Active: true),
                    CancellationToken.None).AsTask()).ConfigureAwait(false);
            var afterFailedUpdate = (await store.GetDomainsAsync(CancellationToken.None).ConfigureAwait(false))
                .Single(domain => domain.Id == secondId);
            Assert.AreEqual("other.example", afterFailedUpdate.Name);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task TransactionDeleteAllDomainsForRestore_CommitRemovesPopulatedDomainGraph()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_domain_restore_commit_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            await using (var transaction = await new SqlServerBackupRestoreMetadataTransactionFactory(
                new SqlServerConnectionFactory(testConnectionString)).BeginAsync(CancellationToken.None))
            {
                await transaction.DeleteAllDomainsForRestoreAsync(CancellationToken.None).ConfigureAwait(false);
                await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            foreach (var (table, column) in DomainGraphRows())
            {
                Assert.AreEqual(0, await CountRowsAsync(testConnectionString, table, column, 1).ConfigureAwait(false), table);
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
    public async Task TransactionDeleteAllDomainsForRestore_DisposalRollsBackPopulatedDomainGraph()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_domain_restore_rollback_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            await using (var transaction = await new SqlServerBackupRestoreMetadataTransactionFactory(
                new SqlServerConnectionFactory(testConnectionString)).BeginAsync(CancellationToken.None))
            {
                await transaction.DeleteAllDomainsForRestoreAsync(CancellationToken.None).ConfigureAwait(false);
            }

            foreach (var (table, column) in DomainGraphRows())
            {
                Assert.AreEqual(1, await CountRowsAsync(testConnectionString, table, column, 1).ConfigureAwait(false), table);
            }
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<(string Table, string Column)> DomainGraphRows() =>
    [
        ("hm_domains", "domainid"),
        ("hm_accounts", "accountdomainid"),
        ("hm_aliases", "aliasdomainid"),
        ("hm_distributionlists", "distributionlistdomainid"),
        ("hm_distributionlistsrecipients", "distributionlistrecipientlistid"),
        ("hm_domain_aliases", "dadomainid"),
        ("hm_rules", "ruleaccountid"),
        ("hm_rule_criterias", "criteriaruleid"),
        ("hm_rule_actions", "actionruleid"),
        ("hm_messages", "messageaccountid"),
        ("hm_messagerecipients", "recipientmessageid"),
        ("hm_message_metadata", "metadata_accountid"),
        ("hm_message_search_queue", "messageid"),
        ("hm_message_search_documents", "messageid"),
        ("hm_acl", "aclpermissionaccountid"),
        ("hm_group_members", "memberaccountid"),
        ("hm_imapfolders", "folderaccountid"),
        ("hm_fetchaccounts_uids", "uidfaid"),
        ("hm_fetchaccounts", "faaccountid")
    ];

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
            CREATE TABLE dbo.hm_domains (
                domainid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                domainname nvarchar(255) NOT NULL,
                domainactive tinyint NOT NULL,
                domainpostmaster nvarchar(255) NOT NULL,
                domainmaxmessagesize int NOT NULL,
                domainuseplusaddressing tinyint NOT NULL,
                domainplusaddressingchar nvarchar(1) NOT NULL,
                domainaddomain nvarchar(255) NOT NULL,
                domainmaxsize int NOT NULL,
                domainmaxnoofaccounts int NOT NULL,
                domainmaxnoofaliases int NOT NULL,
                domainmaxnoofdistributionlists int NOT NULL,
                domainlimitationsenabled tinyint NOT NULL,
                domainmaxaccountsize int NOT NULL,
                domainenablesignature tinyint NOT NULL,
                domainsignaturemethod tinyint NOT NULL,
                domainsignatureplaintext nvarchar(max) NOT NULL,
                domainsignaturehtml nvarchar(max) NOT NULL,
                domainaddsignaturestoreplies tinyint NOT NULL,
                domainaddsignaturestolocalemail tinyint NOT NULL,
                domainantispamoptions int NOT NULL,
                domaindkimselector nvarchar(255) NOT NULL,
                domaindkimprivatekeyfile nvarchar(255) NOT NULL
            );
            CREATE TABLE dbo.hm_accounts (
                accountid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                accountdomainid int NOT NULL,
                accountmaxsize int NOT NULL
            );
            CREATE TABLE dbo.hm_aliases (
                aliasid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                aliasdomainid int NOT NULL,
                aliasname nvarchar(255) NOT NULL
            );
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
            CREATE TABLE dbo.hm_domain_aliases (
                daid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                dadomainid int NOT NULL,
                daalias nvarchar(255) NOT NULL
            );
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
                criteriaruleid int NOT NULL
            );
            CREATE TABLE dbo.hm_rule_actions (
                actionid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                actionruleid int NOT NULL
            );
            CREATE TABLE dbo.hm_messages (
                messageid bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                messageaccountid int NOT NULL,
                messagesize bigint NOT NULL
            );
            CREATE TABLE dbo.hm_messagerecipients (
                recipientid bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                recipientmessageid bigint NOT NULL
            );
            CREATE TABLE dbo.hm_message_metadata (
                metadata_id bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                metadata_accountid int NOT NULL
            );
            CREATE TABLE dbo.hm_message_search_queue (
                messageid bigint NOT NULL PRIMARY KEY,
                queuedutc datetime2(3) NOT NULL,
                attempts int NOT NULL,
                lastattemptutc datetime2(3) NULL,
                nextattemptutc datetime2(3) NULL,
                searchleaseowner nvarchar(128) NULL,
                searchleaseexpiresutc datetime2(3) NULL,
                lasterror nvarchar(1024) NULL
            );
            CREATE TABLE dbo.hm_message_search_documents (
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
            CREATE TABLE dbo.hm_imapfolders (
                folderid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                folderaccountid int NOT NULL,
                folderparentid int NOT NULL,
                foldername nvarchar(255) NOT NULL,
                folderissubscribed tinyint NOT NULL,
                foldercreationtime datetime NOT NULL,
                foldercurrentuid bigint NOT NULL
            );
            CREATE TABLE dbo.hm_group_members (
                memberid bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                membergroupid bigint NOT NULL,
                memberaccountid bigint NOT NULL
            );
            CREATE TABLE dbo.hm_acl (
                aclid bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                aclsharefolderid bigint NOT NULL,
                aclpermissiontype tinyint NOT NULL,
                aclpermissiongroupid bigint NOT NULL,
                aclpermissionaccountid bigint NOT NULL,
                aclvalue bigint NOT NULL
            );
            CREATE TABLE dbo.hm_fetchaccounts (
                faid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                faaccountid int NOT NULL
            );
            CREATE TABLE dbo.hm_fetchaccounts_uids (
                uidid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                uidfaid int NOT NULL,
                uidvalue nvarchar(255) NOT NULL,
                uidtime datetime NOT NULL
            );
            INSERT INTO dbo.hm_domains
                (domainname, domainactive, domainpostmaster, domainmaxmessagesize, domainuseplusaddressing,
                 domainplusaddressingchar, domainaddomain, domainmaxsize, domainmaxnoofaccounts,
                 domainmaxnoofaliases, domainmaxnoofdistributionlists, domainlimitationsenabled,
                 domainmaxaccountsize, domainenablesignature, domainsignaturemethod, domainsignatureplaintext,
                 domainsignaturehtml, domainaddsignaturestoreplies, domainaddsignaturestolocalemail,
                 domainantispamoptions, domaindkimselector, domaindkimprivatekeyfile)
            VALUES
                (N'seeded.example', 1, N'', 0, 0, N'+', N'', 0, 0, 0, 0, 0, 0, 0, 1, N'', N'', 0, 1, 1, N'', N'');
            INSERT INTO dbo.hm_accounts (accountdomainid, accountmaxsize) VALUES (1, 0);
            INSERT INTO dbo.hm_aliases (aliasdomainid, aliasname) VALUES (1, N'alias@seeded.example');
            INSERT INTO dbo.hm_distributionlists (distributionlistdomainid, distributionlistaddress) VALUES (1, N'list@seeded.example');
            INSERT INTO dbo.hm_distributionlistsrecipients (distributionlistrecipientlistid, distributionlistrecipientaddress) VALUES (1, N'r@seeded.example');
            INSERT INTO dbo.hm_domain_aliases (dadomainid, daalias) VALUES (1, N'alt.seeded.example');
            INSERT INTO dbo.hm_rules (ruleaccountid, rulename, ruleactive, ruleuseand, rulesortorder) VALUES (1, N'r', 1, 1, 0);
            INSERT INTO dbo.hm_rule_criterias (criteriaruleid) VALUES (1);
            INSERT INTO dbo.hm_rule_actions (actionruleid) VALUES (1);
            INSERT INTO dbo.hm_messages (messageaccountid, messagesize) VALUES (1, 100);
            INSERT INTO dbo.hm_messagerecipients (recipientmessageid) VALUES (1);
            INSERT INTO dbo.hm_message_metadata (metadata_accountid) VALUES (1);
            INSERT INTO dbo.hm_message_search_queue
                (messageid, queuedutc, attempts)
            VALUES
                (1, SYSUTCDATETIME(), 0);
            INSERT INTO dbo.hm_message_search_documents
                (messageid, messageaccountid, messagefolderid, messageuid, messageinternaldateutc,
                 messagesize, messageflags, search_header, search_body, search_combined, updatedutc)
            VALUES
                (1, 1, 1, 1, SYSUTCDATETIME(), 100, 0, N'', N'', N'', SYSUTCDATETIME());
            INSERT INTO dbo.hm_imapfolders
                (folderaccountid, folderparentid, foldername, folderissubscribed, foldercreationtime, foldercurrentuid)
            VALUES
                (1, -1, N'INBOX', 1, SYSUTCDATETIME(), 1);
            INSERT INTO dbo.hm_group_members (membergroupid, memberaccountid) VALUES (1, 1);
            INSERT INTO dbo.hm_acl
                (aclsharefolderid, aclpermissiontype, aclpermissiongroupid, aclpermissionaccountid, aclvalue)
            VALUES
                (1, 0, 0, 1, 1);
            INSERT INTO dbo.hm_fetchaccounts (faaccountid) VALUES (1);
            INSERT INTO dbo.hm_fetchaccounts_uids (uidfaid, uidvalue, uidtime)
            VALUES (1, N'uid-1', SYSUTCDATETIME());
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<long> CountRowsAsync(
        string connectionString,
        string tableName,
        string columnName,
        int value)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            $"SELECT COUNT_BIG(*) FROM dbo.{tableName} WHERE {columnName} = @Value;",
            connection);
        command.Parameters.Add("@Value", SqlDbType.Int).Value = value;
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
