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
            var authConnectionBuilder = new SqlConnectionStringBuilder(testConnectionString)
            {
                MaxPoolSize = 1,
                ConnectTimeout = 2
            };
            var authConnectionString = authConnectionBuilder.ConnectionString;
            var calls = new List<(string Domain, string Username, string Password)>();
            var validator = new DelegateActiveDirectoryPasswordValidator((domain, username, password) =>
            {
                calls.Add((domain, username, password));
                using var probeConnection = new SqlConnection(authConnectionString);
                probeConnection.Open();
                using var probeCommand = new SqlCommand("SELECT 1;", probeConnection);
                _ = probeCommand.ExecuteScalar();
                return username == "ada";
            });
            var authenticator = new SqlServerImapAccountAuthenticator(
                new SqlServerConnectionFactory(authConnectionString),
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

            var defaultDomainAuthenticator = new SqlServerImapAccountAuthenticator(
                new SqlServerConnectionFactory(authConnectionString),
                settingsAdministrationStore: new FixedSettingsAdministrationStore("example.test"),
                activeDirectoryPasswordValidator: new DelegateActiveDirectoryPasswordValidator(
                    (_, username, _) => username == "default"));
            var defaultDomainResult = await defaultDomainAuthenticator
                .AuthenticateAsync("default", "directory-secret", CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsTrue(defaultDomainResult.Succeeded);
            Assert.IsNotNull(defaultDomainResult.Account);
            Assert.AreEqual("default@example.test", defaultDomainResult.Account!.Address);

            var aliasCalls = new List<(string Domain, string Username)>();
            var aliasAuthenticator = new SqlServerImapAccountAuthenticator(
                new SqlServerConnectionFactory(authConnectionString),
                activeDirectoryPasswordValidator: new DelegateActiveDirectoryPasswordValidator(
                    (domain, username, _) =>
                    {
                        aliasCalls.Add((domain, username));
                        return true;
                    }));
            var aliasResult = await aliasAuthenticator
                .AuthenticateAsync("ALIASUSER@ALIAS.TEST", "directory-secret", CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsTrue(aliasResult.Succeeded, aliasResult.FailureMessage);
            Assert.IsNotNull(aliasResult.Account);
            Assert.AreEqual("aliasuser@example.test", aliasResult.Account!.Address);
            CollectionAssert.AreEqual(
                new[] { (Domain: "CORP", Username: "aliasuser") },
                aliasCalls);

            var quotedLocalPartAuthenticator = new SqlServerImapAccountAuthenticator(
                new SqlServerConnectionFactory(authConnectionString),
                activeDirectoryPasswordValidator: new DelegateActiveDirectoryPasswordValidator(
                    (_, username, _) => username == "quoted"));
            var quotedLocalPartResult = await quotedLocalPartAuthenticator
                .AuthenticateAsync("\"a@b\"@ALIAS.TEST", "directory-secret", CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsTrue(quotedLocalPartResult.Succeeded, quotedLocalPartResult.FailureMessage);
            Assert.IsNotNull(quotedLocalPartResult.Account);
            Assert.AreEqual("\"a@b\"@example.test", quotedLocalPartResult.Account!.Address);

            var plainAliasAuthenticator = new SqlServerImapAccountAuthenticator(
                new SqlServerConnectionFactory(authConnectionString));
            var plainAliasResult = await plainAliasAuthenticator
                .AuthenticateAsync("aliaslocal@ALIAS.TEST", "secret", CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsTrue(plainAliasResult.Succeeded, plainAliasResult.FailureMessage);
            Assert.IsNotNull(plainAliasResult.Account);
            Assert.AreEqual("aliaslocal@example.test", plainAliasResult.Account!.Address);

            var inactiveDomain = await authenticator
                .AuthenticateAsync("inactive@example.test", "directory-secret", CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsFalse(inactiveDomain.Succeeded);
            Assert.AreEqual("Invalid user name or password.", inactiveDomain.FailureMessage);
            Assert.AreEqual(2, calls.Count, "An inactive domain must not reach the AD validator.");
            Assert.AreEqual(
                new DateTime(2000, 1, 1),
                await ReadLastLogonAsync(testConnectionString, 3).ConfigureAwait(false));

            var scriptPasswords = new List<string>();
            var scriptAuthenticator = new SqlServerImapAccountAuthenticator(
                new SqlServerConnectionFactory(authConnectionString),
                passwordValidationScriptExecutor: new DelegatePasswordValidationScriptExecutor(
                    request =>
                    {
                        scriptPasswords.Add(request.Password);
                        return ClientPasswordValidationScriptResult.Accept();
                    }),
                activeDirectoryPasswordValidator: validator);
            var scriptBaseline = await ReadLastLogonAsync(testConnectionString, 4).ConfigureAwait(false);
            var scriptAccepted = await scriptAuthenticator
                .AuthenticateAsync("script@example.test", string.Empty, CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsTrue(scriptAccepted.Succeeded);
            CollectionAssert.AreEqual(new[] { string.Empty }, scriptPasswords);
            Assert.IsTrue(
                await ReadLastLogonAsync(testConnectionString, 4).ConfigureAwait(false)
                    > scriptBaseline);

            var continueValidatorCalls = 0;
            var continuedAuthenticator = new SqlServerImapAccountAuthenticator(
                new SqlServerConnectionFactory(authConnectionString),
                passwordValidationScriptExecutor: new DelegatePasswordValidationScriptExecutor(
                    _ => ClientPasswordValidationScriptResult.Continue()),
                activeDirectoryPasswordValidator: new DelegateActiveDirectoryPasswordValidator(
                    (_, _, _) =>
                    {
                        continueValidatorCalls++;
                        return true;
                    }));
            var continued = await continuedAuthenticator
                .AuthenticateAsync("rejected@example.test", string.Empty, CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsFalse(continued.Succeeded);
            Assert.AreEqual("Invalid user name or password.", continued.FailureMessage);
            Assert.AreEqual(0, continueValidatorCalls);
            Assert.AreEqual(
                new DateTime(2000, 1, 1),
                await ReadLastLogonAsync(testConnectionString, 2).ConfigureAwait(false));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task AuthenticateMasterUser_UpdatesOnlyResolvedTargetAndRejectsInvalidMasterOrTarget()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_imap_master_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var authenticator = new SqlServerImapAccountAuthenticator(
                new SqlServerConnectionFactory(testConnectionString),
                settingsAdministrationStore: new FixedSettingsAdministrationStore(
                    "example.test",
                    "master"));
            var sentinel = new DateTime(2000, 1, 1);

            var directSuccess = await authenticator
                .AuthenticateAsync(
                    "master@example.test",
                    "master-secret",
                    "ALIASLOCAL@EXAMPLE.TEST",
                    CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsTrue(directSuccess.Succeeded, directSuccess.FailureMessage);
            Assert.IsNotNull(directSuccess.Account);
            Assert.AreEqual("aliaslocal@example.test", directSuccess.Account!.Address);
            Assert.AreEqual(sentinel, await ReadLastLogonAsync(testConnectionString, 9).ConfigureAwait(false));
            Assert.IsTrue(await ReadLastLogonAsync(testConnectionString, 8).ConfigureAwait(false) > sentinel);

            await ResetLastLogonsAsync(testConnectionString).ConfigureAwait(false);
            var aliasSuccess = await authenticator
                .AuthenticateAsync(
                    "master@example.test",
                    "master-secret",
                    "\"a@b\"@ALIAS.TEST",
                    CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsTrue(aliasSuccess.Succeeded, aliasSuccess.FailureMessage);
            Assert.IsNotNull(aliasSuccess.Account);
            Assert.AreEqual("\"a@b\"@example.test", aliasSuccess.Account!.Address);
            Assert.AreEqual(sentinel, await ReadLastLogonAsync(testConnectionString, 9).ConfigureAwait(false));
            Assert.IsTrue(await ReadLastLogonAsync(testConnectionString, 7).ConfigureAwait(false) > sentinel);

            await ResetLastLogonsAsync(testConnectionString).ConfigureAwait(false);
            var unknownTarget = await authenticator
                .AuthenticateAsync(
                    "master@example.test",
                    "master-secret",
                    "unknown@example.test",
                    CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsFalse(unknownTarget.Succeeded);
            Assert.AreEqual("Invalid user name or password.", unknownTarget.FailureMessage);
            Assert.AreEqual(sentinel, await ReadLastLogonAsync(testConnectionString, 9).ConfigureAwait(false));
            Assert.AreEqual(sentinel, await ReadLastLogonAsync(testConnectionString, 8).ConfigureAwait(false));

            var invalidMaster = await authenticator
                .AuthenticateAsync(
                    "master@example.test",
                    "wrong-secret",
                    "aliaslocal@example.test",
                    CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsFalse(invalidMaster.Succeeded);
            Assert.AreEqual("Invalid user name or password.", invalidMaster.FailureMessage);
            Assert.AreEqual(sentinel, await ReadLastLogonAsync(testConnectionString, 9).ConfigureAwait(false));
            Assert.AreEqual(sentinel, await ReadLastLogonAsync(testConnectionString, 8).ConfigureAwait(false));

            var inactiveTarget = await authenticator
                .AuthenticateAsync(
                    "master@example.test",
                    "master-secret",
                    "inactive@example.test",
                    CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsFalse(inactiveTarget.Succeeded);
            Assert.AreEqual("Invalid user name or password.", inactiveTarget.FailureMessage);
            Assert.AreEqual(sentinel, await ReadLastLogonAsync(testConnectionString, 9).ConfigureAwait(false));
            Assert.AreEqual(sentinel, await ReadLastLogonAsync(testConnectionString, 3).ConfigureAwait(false));
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
            CREATE TABLE dbo.hm_domain_aliases (
                daid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                dadomainid int NOT NULL,
                daalias nvarchar(255) NOT NULL
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
            INSERT INTO dbo.hm_domain_aliases (dadomainid, daalias)
            VALUES (10, N'alias.test');
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
                 N'', 0, 0, 0, N'', N'', '2000-01-01T00:00:00', N'', N''),
                (10, N'script@example.test', N'', 1, 1, N'CORP', N'script', 0, 0, N'', N'', 0, N'', 0, 0, 0, 0,
                 N'', 0, 0, 0, N'', N'', '2000-01-01T00:00:00', N'', N''),
                (10, N'default@example.test', N'', 1, 1, N'CORP', N'default', 0, 0, N'', N'', 0, N'', 0, 0, 0, 0,
                 N'', 0, 0, 0, N'', N'', '2000-01-01T00:00:00', N'', N''),
                (10, N'aliasuser@example.test', N'', 1, 1, N'CORP', N'aliasuser', 0, 0, N'', N'', 0, N'', 0, 0, 0, 0,
                 N'', 0, 0, 0, N'', N'', '2000-01-01T00:00:00', N'', N''),
                (10, N'"a@b"@example.test', N'', 1, 1, N'CORP', N'quoted', 0, 0, N'', N'', 0, N'', 0, 0, 0, 0,
                 N'', 0, 0, 0, N'', N'', '2000-01-01T00:00:00', N'', N''),
                (10, N'aliaslocal@example.test', N'secret', 1, 0, N'', N'', 0, 0, N'', N'', 0, N'', 0, 0, 0, 0,
                 N'', 0, 0, 0, N'', N'', '2000-01-01T00:00:00', N'', N''),
                (10, N'master@example.test', N'master-secret', 1, 0, N'', N'', 0, 0, N'', N'', 0, N'', 0, 0, 0, 0,
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

    private static async Task ResetLastLogonsAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "UPDATE dbo.hm_accounts SET accountlastlogontime = @LastLogon WHERE accountid IN (3, 7, 8, 9);",
            connection);
        command.Parameters.Add("@LastLogon", System.Data.SqlDbType.DateTime).Value =
            new DateTime(2000, 1, 1);
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

    private sealed class DelegatePasswordValidationScriptExecutor : IClientPasswordValidationScriptExecutor
    {
        private readonly Func<ClientPasswordValidationScriptRequest, ClientPasswordValidationScriptResult> _execute;

        public DelegatePasswordValidationScriptExecutor(
            Func<ClientPasswordValidationScriptRequest, ClientPasswordValidationScriptResult> execute)
        {
            _execute = execute;
        }

        public ClientPasswordValidationScriptResult Execute(
            ClientPasswordValidationScriptRequest request,
            CancellationToken cancellationToken) =>
            _execute(request);
    }

    private sealed class FixedSettingsAdministrationStore : ISettingsAdministrationStore
    {
        private readonly SettingsAdministrationSnapshot _settings;

        public FixedSettingsAdministrationStore(string defaultDomain, string imapMasterUser = "")
        {
            _settings = new SettingsAdministrationSnapshot(
                "host",
                "smtp",
                "pop3",
                "imap",
                DefaultDomain: defaultDomain,
                ImapMasterUser: imapMasterUser);
        }

        public ValueTask<SettingsAdministrationSnapshot> GetSettingsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_settings);
    }
}
