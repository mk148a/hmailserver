using HMailServer.ComInterop;
using HMailServer.Security;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerMessageIndexingIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task AuthenticatedComPath_ExecutesMessageIndexingAdministrationAgainstIsolatedDatabase()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        var databaseName = $"hmailserver_net10_test_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var store = new SqlServerMessageIndexingAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString));
            MessageIndexingRuntimeHost.Configure(new StoreBackedMessageIndexingRuntime(store));
            var application = Application.CreateForRuntime(
                new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"));

            Assert.IsNull(application.Authenticate("Administrator", "wrong"));
            Assert.IsNotNull(application.Authenticate("administrator", "secret"));
            var indexing = application.Settings.MessageIndexing;
            var extended = (IInterfaceMessageIndexing2)indexing;

            Assert.AreEqual(2, indexing.TotalMessageCount);
            Assert.AreEqual(1, indexing.TotalIndexedCount);
            Assert.IsFalse(indexing.Enabled);
            Assert.AreEqual("Queued=0", extended.BackfillStatus);

            indexing.Enabled = true;

            Assert.IsTrue(indexing.Enabled);
            Assert.AreEqual("Queued=1", extended.BackfillStatus);

            indexing.Clear();

            Assert.AreEqual(0, indexing.TotalIndexedCount);
            Assert.AreEqual("Queued=2", extended.BackfillStatus);

            indexing.Index();

            Assert.AreEqual("Queued=2", extended.BackfillStatus);
            indexing.Enabled = false;
            Assert.IsFalse(indexing.Enabled);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task AuthenticatedComPath_ExecutesDomainLookupAgainstIsolatedDatabase()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        var databaseName = $"hmailserver_net10_test_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            await CreateDomainSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            DomainAdministrationRuntimeHost.Configure(
                new SqlServerDomainAdministrationStore(
                    new SqlServerConnectionFactory(testConnectionString)));
            var application = Application.CreateForRuntime(
                new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"));

            Assert.IsNotNull(application.Authenticate("administrator", "secret"));
            var domains = application.Domains;

            Assert.AreEqual(2, domains.Count);
            Assert.AreEqual("alpha.example", domains[0].Name);
            Assert.AreEqual("beta.example", domains.get_ItemByName("BETA.EXAMPLE").Name);
            Assert.IsFalse(domains.get_ItemByDBID(20).Active);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task AuthenticatedComPath_ExecutesAccountLookupAgainstIsolatedDatabase()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        var databaseName = $"hmailserver_net10_test_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            await CreateDomainAndAccountSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var connectionFactory = new SqlServerConnectionFactory(testConnectionString);
            DomainAdministrationRuntimeHost.Configure(new SqlServerDomainAdministrationStore(connectionFactory));
            AccountAdministrationRuntimeHost.Configure(new SqlServerAccountAdministrationStore(connectionFactory));
            var application = Application.CreateForRuntime(
                new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"));

            Assert.IsNotNull(application.Authenticate("administrator", "secret"));
            var domain = application.Domains.get_ItemByName("example.test");
            var accounts = domain.Accounts;

            Assert.AreEqual(2, accounts.Count);
            Assert.AreEqual("admin@example.test", accounts[0].Address);
            Assert.AreEqual(10, accounts[0].DomainID);
            Assert.AreEqual(ComAdminLevel.ServerAdministrator, accounts[0].AdminLevel);
            Assert.AreEqual("user@example.test", accounts.get_ItemByAddress("USER@EXAMPLE.TEST").Address);
            Assert.IsFalse(accounts.get_ItemByDBID(20).Active);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task AuthenticatedComPath_ExecutesAliasLookupAgainstIsolatedDatabase()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        var databaseName = $"hmailserver_net10_test_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            await CreateDomainAndAliasSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var connectionFactory = new SqlServerConnectionFactory(testConnectionString);
            DomainAdministrationRuntimeHost.Configure(new SqlServerDomainAdministrationStore(connectionFactory));
            AliasAdministrationRuntimeHost.Configure(new SqlServerAliasAdministrationStore(connectionFactory));
            var application = Application.CreateForRuntime(
                new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"));

            Assert.IsNotNull(application.Authenticate("administrator", "secret"));
            var domain = application.Domains.get_ItemByName("example.test");
            var aliases = domain.Aliases;

            Assert.AreEqual(2, aliases.Count);
            Assert.AreEqual("abuse@example.test", aliases[0].Name);
            Assert.AreEqual("admin@example.test", aliases[0].Value);
            Assert.AreEqual(10, aliases[0].DomainID);
            Assert.AreEqual("sales@example.test", aliases.get_ItemByName("SALES@EXAMPLE.TEST").Name);
            Assert.IsFalse(aliases.get_ItemByDBID(20).Active);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
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
CREATE TABLE dbo.hm_messages
(
    messageid bigint NOT NULL PRIMARY KEY,
    messagetype int NOT NULL
);

CREATE TABLE dbo.hm_settings
(
    settingname nvarchar(255) NOT NULL PRIMARY KEY,
    settingstring nvarchar(max) NOT NULL,
    settinginteger int NOT NULL
);

CREATE TABLE dbo.hm_message_search_documents
(
    messageid bigint NOT NULL PRIMARY KEY
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

INSERT INTO dbo.hm_messages (messageid, messagetype)
VALUES (1, 2), (2, 2), (3, 3);

INSERT INTO dbo.hm_message_search_documents (messageid)
VALUES (1);
""";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task CreateDomainSchemaAndSeedAsync(string connectionString)
    {
        const string sql = """
CREATE TABLE dbo.hm_domains
(
    domainid int NOT NULL PRIMARY KEY,
    domainname nvarchar(80) NOT NULL,
    domainactive tinyint NOT NULL
);

INSERT INTO dbo.hm_domains (domainid, domainname, domainactive)
VALUES
    (20, N'beta.example', 0),
    (10, N'alpha.example', 1);
""";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task CreateDomainAndAccountSchemaAndSeedAsync(string connectionString)
    {
        const string sql = """
CREATE TABLE dbo.hm_domains
(
    domainid int NOT NULL PRIMARY KEY,
    domainname nvarchar(80) NOT NULL,
    domainactive tinyint NOT NULL
);

CREATE TABLE dbo.hm_accounts
(
    accountid int NOT NULL PRIMARY KEY,
    accountdomainid int NOT NULL,
    accountaddress nvarchar(255) NOT NULL,
    accountactive tinyint NOT NULL,
    accountadminlevel tinyint NOT NULL
);

INSERT INTO dbo.hm_domains (domainid, domainname, domainactive)
VALUES
    (10, N'example.test', 1),
    (30, N'other.test', 1);

INSERT INTO dbo.hm_accounts (accountid, accountdomainid, accountaddress, accountactive, accountadminlevel)
VALUES
    (20, 10, N'user@example.test', 0, 0),
    (10, 10, N'admin@example.test', 1, 2),
    (30, 30, N'outside@other.test', 1, 0);
""";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task CreateDomainAndAliasSchemaAndSeedAsync(string connectionString)
    {
        const string sql = """
CREATE TABLE dbo.hm_domains
(
    domainid int NOT NULL PRIMARY KEY,
    domainname nvarchar(80) NOT NULL,
    domainactive tinyint NOT NULL
);

CREATE TABLE dbo.hm_aliases
(
    aliasid int NOT NULL PRIMARY KEY,
    aliasdomainid int NOT NULL,
    aliasname nvarchar(255) NOT NULL,
    aliasvalue nvarchar(255) NOT NULL,
    aliasactive tinyint NOT NULL
);

INSERT INTO dbo.hm_domains (domainid, domainname, domainactive)
VALUES
    (10, N'example.test', 1),
    (30, N'other.test', 1);

INSERT INTO dbo.hm_aliases (aliasid, aliasdomainid, aliasname, aliasvalue, aliasactive)
VALUES
    (20, 10, N'sales@example.test', N'user@example.test', 0),
    (10, 10, N'abuse@example.test', N'admin@example.test', 1),
    (30, 30, N'outside@other.test', N'outside-target@other.test', 1);
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
