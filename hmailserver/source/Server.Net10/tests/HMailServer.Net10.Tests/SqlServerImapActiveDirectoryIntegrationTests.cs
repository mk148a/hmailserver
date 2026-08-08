using System.Data;
using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerImapActiveDirectoryIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task AuthenticateActiveDirectoryAccounts_UsesSqlStateAndContainedValidatorBoundary()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_ad_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var calls = new List<(string Domain, string Username, string Password)>();
            var validator = new DelegateActiveDirectoryPasswordValidator((domain, username, password) =>
            {
                calls.Add((domain, username, password));
                return username == "ada";
            });
            var authenticator = new SqlServerImapAccountAuthenticator(
                new SqlServerConnectionFactory(testConnectionString),
                activeDirectoryPasswordValidator: validator);

            var valid = await authenticator
                .AuthenticateAsync("ad@example.test", "directory-secret", CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsTrue(valid.Succeeded);
            Assert.IsNotNull(valid.Account);
            Assert.AreEqual("ad@example.test", valid.Account!.Address);
            CollectionAssert.AreEqual(
                new[] { (Domain: "CORP", Username: "ada", Password: "directory-secret") },
                calls);
            Assert.IsTrue(
                await ReadLastLogonAsync(testConnectionString, 1).ConfigureAwait(false)
                    > new DateTime(2000, 1, 1));

            var rejected = await authenticator
                .AuthenticateAsync("rejected@example.test", "wrong-secret", CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsFalse(rejected.Succeeded);
            Assert.AreEqual("Invalid user name or password.", rejected.FailureMessage);
            Assert.AreEqual(2, calls.Count);
            Assert.AreEqual("CORP", calls[1].Domain);
            Assert.AreEqual("rejected", calls[1].Username);
            Assert.AreEqual("wrong-secret", calls[1].Password);
            Assert.AreEqual(
                new DateTime(2000, 1, 1),
                await ReadLastLogonAsync(testConnectionString, 2).ConfigureAwait(false));

            var inactiveDomain = await authenticator
                .AuthenticateAsync("inactive@example.test", "directory-secret", CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsFalse(inactiveDomain.Succeeded);
            Assert.AreEqual("Invalid user name or password.", inactiveDomain.FailureMessage);
            Assert.AreEqual(2, calls.Count, "An inactive domain must not reach the AD validator.");
            Assert.AreEqual(
                new DateTime(2000, 1, 1),
                await ReadLastLogonAsync(testConnectionString, 3).ConfigureAwait(false));
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
                domainid int NOT NULL PRIMARY KEY,
                domainname nvarchar(255) NOT NULL,
                domainactive tinyint NOT NULL
            );
            CREATE TABLE dbo.hm_accounts (
                accountid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                accountdomainid int NOT NULL,
                accountaddress nvarchar(255) NOT NULL,
                accountpassword nvarchar(255) NOT NULL,
                accountactive int NOT NULL,
                accountisad int NOT NULL,
                accountaddomain nvarchar(255) NOT NULL,
                accountadusername nvarchar(255) NOT NULL,
                accountmaxsize int NOT NULL,
                accountvacationmessageon tinyint NOT NULL,
                accountvacationmessage nvarchar(1000) NOT NULL,
                accountvacationsubject nvarchar(200) NOT NULL,
                accountvacationexpires tinyint NOT NULL,
                accountvacationexpiredate datetime NOT NULL,
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
            INSERT INTO dbo.hm_domains (domainid, domainname, domainactive)
            VALUES (10, N'example.test', 1), (20, N'inactive.test', 0);
            INSERT INTO dbo.hm_accounts
                (accountdomainid, accountaddress, accountpassword, accountactive, accountisad, accountaddomain,
                 accountadusername, accountmaxsize, accountvacationmessageon, accountvacationmessage,
                 accountvacationsubject, accountvacationexpires, accountvacationexpiredate,
                 accountvacationabortspamflagged, accountpwencryption, accountadminlevel, accountforwardenabled,
                 accountforwardaddress, accountforwardkeeporiginal, accountforwardabortspamflagged,
                 accountenablesignature, accountsignatureplaintext, accountsignaturehtml, accountlastlogontime,
                 accountpersonfirstname, accountpersonlastname)
            VALUES
                (10, N'ad@example.test', N'', 1, 1, N'CORP', N'ada', 0, 0, N'', N'', 0, N'', 0, 0, 0, 0,
                 N'', 0, 0, 0, N'', N'', '2000-01-01T00:00:00', N'', N''),
                (10, N'rejected@example.test', N'', 1, 1, N'CORP', N'rejected', 0, 0, N'', N'', 0, N'', 0, 0, 0, 0,
                 N'', 0, 0, 0, N'', N'', '2000-01-01T00:00:00', N'', N''),
                (20, N'inactive@example.test', N'', 1, 1, N'CORP', N'inactive', 0, 0, N'', N'', 0, N'', 0, 0, 0, 0,
                 N'', 0, 0, 0, N'', N'', '2000-01-01T00:00:00', N'', N'');
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<DateTime> ReadLastLogonAsync(string connectionString, int accountId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT accountlastlogontime FROM dbo.hm_accounts WHERE accountid = @AccountId;",
            connection);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
        return (DateTime)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
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

    private sealed class DelegateActiveDirectoryPasswordValidator : IActiveDirectoryPasswordValidator
    {
        private readonly Func<string, string, string, bool> _validate;

        public DelegateActiveDirectoryPasswordValidator(Func<string, string, string, bool> validate)
        {
            _validate = validate;
        }

        public bool Validate(string domain, string username, string password) =>
            _validate(domain, username, password);
    }
}
