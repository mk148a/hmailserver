using System.Data;
using System.Globalization;
using HMailServer.Security;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerBackupProjectionIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task BackupProjections_FeedArchiveFromIsolatedDatabase()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_backup_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            await OverwriteEncryptedPasswordAsync(testConnectionString, 1, "secret").ConfigureAwait(false);
            var factory = new SqlServerConnectionFactory(testConnectionString);
            var accountStore = new SqlServerAccountAdministrationStore(factory);
            var ruleStore = new SqlServerRuleAdministrationStore(factory);

            var accounts = await accountStore.GetBackupAccountsAsync(1, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(1, accounts.Count);
            var account = accounts[0];
            Assert.AreEqual(1, account.Account.Id);
            Assert.AreEqual(1, account.Account.DomainId);
            Assert.AreEqual("archive@example.test", account.Account.Address);
            Assert.AreEqual(1, account.PasswordEncryption);
            Assert.IsTrue(LegacyBlowfishPasswordCipher.TryDecrypt(account.Password, out var decrypted));
            Assert.AreEqual("secret", decrypted);

            var rules = await ruleStore.GetBackupRulesAsync(1, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(1, rules.Count);
            Assert.AreEqual(1, rules[0].Id);
            Assert.AreEqual("archive-rule", rules[0].Name);

            var domains = await new SqlServerDomainAdministrationStore(factory)
                .GetDomainsAsync(CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(1, domains.Count);
            Assert.AreEqual("archive.example", domains[0].Name);
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
                accountaddress nvarchar(255) NOT NULL,
                accountpassword nvarchar(255) NOT NULL,
                accountactive tinyint NOT NULL,
                accountisad tinyint NOT NULL,
                accountaddomain nvarchar(255) NOT NULL,
                accountadusername nvarchar(255) NOT NULL,
                accountmaxsize int NOT NULL,
                accountvacationmessageon tinyint NOT NULL,
                accountvacationmessage nvarchar(1000) NOT NULL,
                accountvacationsubject nvarchar(200) NOT NULL,
                accountvacationexpires tinyint NOT NULL,
                accountvacationexpiredate nvarchar(255) NOT NULL,
                accountvacationabortspamflagged tinyint NOT NULL,
                accountpwencryption tinyint NOT NULL,
                accountadminlevel tinyint NOT NULL,
                accountforwardenabled tinyint NOT NULL,
                accountforwardaddress nvarchar(255) NOT NULL,
                accountforwardkeeporiginal tinyint NOT NULL,
                accountforwardabortspamflagged tinyint NOT NULL,
                accountenablesignature tinyint NOT NULL,
                accountsignatureplaintext nvarchar(max) NOT NULL,
                accountsignaturehtml nvarchar(max) NOT NULL,
                accountlastlogontime datetime NOT NULL,
                accountpersonfirstname nvarchar(60) NOT NULL,
                accountpersonlastname nvarchar(60) NOT NULL
            );
            CREATE TABLE dbo.hm_messages (
                messageid bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                messageaccountid int NOT NULL,
                messagesize bigint NOT NULL
            );
            CREATE TABLE dbo.hm_rules (
                ruleid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                ruleaccountid int NOT NULL,
                rulename nvarchar(100) NOT NULL,
                ruleactive tinyint NOT NULL,
                ruleuseand tinyint NOT NULL,
                rulesortorder int NOT NULL
            );
            INSERT INTO dbo.hm_domains
                (domainname, domainactive, domainpostmaster, domainmaxmessagesize, domainuseplusaddressing,
                 domainplusaddressingchar, domainaddomain, domainmaxsize, domainmaxnoofaccounts,
                 domainmaxnoofaliases, domainmaxnoofdistributionlists, domainlimitationsenabled,
                 domainmaxaccountsize, domainenablesignature, domainsignaturemethod, domainsignatureplaintext,
                 domainsignaturehtml, domainaddsignaturestoreplies, domainaddsignaturestolocalemail,
                 domainantispamoptions, domaindkimselector, domaindkimprivatekeyfile)
            VALUES
                (N'archive.example', 1, N'', 0, 0, N'+', N'', 0, 0, 0, 0, 0, 0, 0, 1, N'', N'', 0, 1, 1, N'', N'');
            INSERT INTO dbo.hm_accounts
                (accountdomainid, accountaddress, accountpassword, accountactive, accountisad, accountaddomain,
                 accountadusername, accountmaxsize, accountvacationmessageon, accountvacationmessage,
                 accountvacationsubject, accountvacationexpires, accountvacationexpiredate,
                 accountvacationabortspamflagged, accountpwencryption, accountadminlevel, accountforwardenabled,
                 accountforwardaddress, accountforwardkeeporiginal, accountforwardabortspamflagged,
                 accountenablesignature, accountsignatureplaintext, accountsignaturehtml, accountlastlogontime,
                 accountpersonfirstname, accountpersonlastname)
            VALUES
                (1, N'archive@example.test', N'', 1, 0, N'', N'', 0, 0, N'', N'', 0, N'', 0, 1, 1, 0, N'', 0, 0, 0, N'', N'', '2026-01-01T00:00:00', N'', N'');
            INSERT INTO dbo.hm_rules (ruleaccountid, rulename, ruleactive, ruleuseand, rulesortorder) VALUES (1, N'archive-rule', 1, 1, 0);
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task OverwriteEncryptedPasswordAsync(
        string connectionString,
        int accountId,
        string password)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "UPDATE dbo.hm_accounts SET accountpassword = @Password WHERE accountid = @AccountID;",
            connection);
        command.Parameters.Add("@Password", SqlDbType.NVarChar, 255).Value =
            LegacyBlowfishPasswordCipher.Encrypt(password);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
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