using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerAccountAdministrationStoreIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task StoreMutations_ExhibitLegacyIdentityReadbackOwnerScopingAndStatementRollback()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_account_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var store = new SqlServerAccountAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString));

            var insertedId = await store.InsertAccountAsync(
                100,
                new AccountAdministrationSnapshot(
                    Id: 0,
                    DomainId: 100,
                    Address: "new@example.test",
                    Active: true,
                    AdminLevel: 2,
                    PersonFirstName: "Ada",
                    PersonLastName: "Lovelace",
                    MaxSize: 512,
                    VacationMessageIsOn: true,
                    VacationMessage: "away",
                    ForwardEnabled: true,
                    ForwardAddress: "fwd@example.test",
                    SignatureEnabled: true,
                    SignaturePlainText: "sig",
                    LastLogonTime: new DateTime(2026, 1, 2, 3, 4, 5)),
                "secret",
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(insertedId > 0);

            var readBack = await store.GetAccountByIdAsync(insertedId, CancellationToken.None).ConfigureAwait(false);
            Assert.IsNotNull(readBack);
            Assert.AreEqual("new@example.test", readBack!.Address);
            Assert.IsTrue(readBack!.Active);
            Assert.AreEqual(2, readBack!.AdminLevel);
            Assert.AreEqual("Ada", readBack!.PersonFirstName);
            Assert.AreEqual("Lovelace", readBack!.PersonLastName);
            Assert.AreEqual(512, readBack!.MaxSize);
            Assert.IsTrue(readBack!.VacationMessageIsOn);
            Assert.IsTrue(readBack!.ForwardEnabled);
            Assert.IsTrue(readBack!.SignatureEnabled);
            Assert.IsTrue(
                LegacyBlowfishPasswordCipher.TryDecrypt(
                    await ReadEncryptedPasswordAsync(testConnectionString, insertedId).ConfigureAwait(false),
                    out var decrypted));
            Assert.AreEqual("secret", decrypted);

            var secondId = await store.InsertAccountAsync(
                100,
                new AccountAdministrationSnapshot(Id: 0, DomainId: 100, Address: "second@example.test", Active: false, AdminLevel: 0, LastLogonTime: new DateTime(2026, 1, 1)),
                string.Empty,
                CancellationToken.None).ConfigureAwait(false);
            Assert.AreNotEqual(insertedId, secondId);
            Assert.AreEqual(3, (await store.GetAccountsAsync(100, CancellationToken.None).ConfigureAwait(false)).Count);

            var wrongOwnerUpdate = await store.UpdateAccountAsync(
                999,
                new AccountAdministrationSnapshot(
                    Id: insertedId,
                    DomainId: 999,
                    Address: "changed@example.test",
                    Active: true,
                    AdminLevel: 2,
                    LastLogonTime: new DateTime(2026, 1, 1)),
                null,
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(wrongOwnerUpdate);
            Assert.AreEqual(
                "new@example.test",
                (await store.GetAccountByIdAsync(insertedId, CancellationToken.None).ConfigureAwait(false))!.Address);

            var ownUpdateNoPassword = await store.UpdateAccountAsync(
                100,
                new AccountAdministrationSnapshot(
                    Id: insertedId,
                    DomainId: 100,
                    Address: "renamed@example.test",
                    Active: false,
                    AdminLevel: 1,
                    MaxSize: 2048,
                    LastLogonTime: new DateTime(2026, 1, 1)),
                null,
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(ownUpdateNoPassword);
            var afterUpdate = (await store.GetAccountByIdAsync(insertedId, CancellationToken.None).ConfigureAwait(false))!;
            Assert.AreEqual("renamed@example.test", afterUpdate.Address);
            Assert.IsFalse(afterUpdate.Active);
            Assert.AreEqual(2048, afterUpdate.MaxSize);
            Assert.IsTrue(
                LegacyBlowfishPasswordCipher.TryDecrypt(
                    await ReadEncryptedPasswordAsync(testConnectionString, insertedId).ConfigureAwait(false),
                    out var unchangedPassword));
            Assert.AreEqual("secret", unchangedPassword);

            var ownUpdateWithPassword = await store.UpdateAccountAsync(
                100,
                new AccountAdministrationSnapshot(
                    Id: insertedId,
                    DomainId: 100,
                    Address: "renamed@example.test",
                    Active: false,
                    AdminLevel: 2,
                    MaxSize: 2048,
                    LastLogonTime: new DateTime(2026, 1, 1)),
                "new-secret",
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(ownUpdateWithPassword);
            Assert.IsTrue(
                LegacyBlowfishPasswordCipher.TryDecrypt(
                    await ReadEncryptedPasswordAsync(testConnectionString, insertedId).ConfigureAwait(false),
                    out var changedPassword));
            Assert.AreEqual("new-secret", changedPassword);

            var wrongOwnerDelete = await store.DeleteAccountAsync(999, 1, CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(wrongOwnerDelete);
            Assert.AreEqual(3, (await store.GetAccountsAsync(100, CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(1, await CountRowsAsync(testConnectionString, "hm_rules", "ruleaccountid", 1).ConfigureAwait(false));

            var cascadeDelete = await store.DeleteAccountAsync(100, 1, CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(cascadeDelete);
            Assert.AreEqual(2, (await store.GetAccountsAsync(100, CancellationToken.None).ConfigureAwait(false)).Count);
            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_rules", "ruleaccountid", 1).ConfigureAwait(false));
            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_messages", "messageaccountid", 1).ConfigureAwait(false));
            Assert.AreEqual(0, await CountRowsAsync(testConnectionString, "hm_fetchaccounts", "faaccountid", 1).ConfigureAwait(false));

            await Assert.ThrowsExactlyAsync<SqlException>(
                () => store.InsertAccountAsync(
                    100,
                    new AccountAdministrationSnapshot(Id: 0, DomainId: 100, Address: null!, Active: true, AdminLevel: 0, LastLogonTime: new DateTime(2026, 1, 1)),
                    string.Empty,
                    CancellationToken.None).AsTask()).ConfigureAwait(false);
            Assert.AreEqual(2, (await store.GetAccountsAsync(100, CancellationToken.None).ConfigureAwait(false)).Count);

            await Assert.ThrowsExactlyAsync<SqlException>(
                () => store.UpdateAccountAsync(
                    100,
                    new AccountAdministrationSnapshot(Id: secondId, DomainId: 100, Address: null!, Active: true, AdminLevel: 0, LastLogonTime: new DateTime(2026, 1, 1)),
                    null,
                    CancellationToken.None).AsTask()).ConfigureAwait(false);
            var afterFailedUpdate = (await store.GetAccountByIdAsync(secondId, CancellationToken.None).ConfigureAwait(false))!;
            Assert.AreEqual("second@example.test", afterFailedUpdate.Address);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RestoreInsert_PreservesArchivePasswordAndEncryptionType()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_restore_account_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var store = new SqlServerAccountAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString));
            var account = new AccountAdministrationSnapshot(
                Id: 0,
                DomainId: 100,
                Address: "archive-encrypted@example.test",
                Active: true,
                AdminLevel: 0,
                LastLogonTime: new DateTime(2026, 1, 1));

            var encryptedId = await store.InsertAccountForRestoreAsync(
                100,
                account,
                "archive-encrypted-value",
                1,
                CancellationToken.None).ConfigureAwait(false);
            var plainId = await store.InsertAccountForRestoreAsync(
                100,
                account with { Address = "archive-plain@example.test" },
                "archive-plain-value",
                0,
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(
                ("archive-encrypted-value", 1),
                await ReadPasswordAndEncryptionAsync(testConnectionString, encryptedId).ConfigureAwait(false));
            Assert.AreEqual(
                ("archive-plain-value", 0),
                await ReadPasswordAndEncryptionAsync(testConnectionString, plainId).ConfigureAwait(false));
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
            CREATE TABLE dbo.hm_messagerecipients (
                recipientid bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                recipientmessageid bigint NOT NULL
            );
            CREATE TABLE dbo.hm_message_metadata (
                metadata_id bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                metadata_accountid int NOT NULL
            );
            CREATE TABLE dbo.hm_message_search_queue (
                messageid bigint NOT NULL PRIMARY KEY
            );
            CREATE TABLE dbo.hm_message_search_documents (
                messageid bigint NOT NULL PRIMARY KEY
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
            CREATE TABLE dbo.hm_fetchaccounts (
                faid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                faaccountid int NOT NULL,
                faaccountname nvarchar(255) NOT NULL
            );
            INSERT INTO dbo.hm_accounts
                (accountdomainid, accountaddress, accountpassword, accountactive, accountisad, accountaddomain,
                 accountadusername, accountmaxsize, accountvacationmessageon, accountvacationmessage,
                 accountvacationsubject, accountvacationexpires, accountvacationexpiredate,
                 accountvacationabortspamflagged, accountpwencryption, accountadminlevel, accountforwardenabled,
                 accountforwardaddress, accountforwardkeeporiginal, accountforwardabortspamflagged,
                 accountenablesignature, accountsignatureplaintext, accountsignaturehtml, accountlastlogontime,
                 accountpersonfirstname, accountpersonlastname)
            VALUES
                (100, N'seed@example.test', N'', 1, 0, N'', N'', 0, 0, N'', N'', 0, N'', 0, 0, 1, 0, N'', 0, 0, 0, N'', N'', '2026-01-01T00:00:00', N'', N'');
            INSERT INTO dbo.hm_rules (ruleaccountid, rulename, ruleactive, ruleuseand, rulesortorder) VALUES (1, N'r', 1, 1, 0);
            INSERT INTO dbo.hm_messages (messageaccountid, messagesize) VALUES (1, 100);
            INSERT INTO dbo.hm_fetchaccounts (faaccountid, faaccountname) VALUES (1, N'fetcher');
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<string> ReadEncryptedPasswordAsync(string connectionString, int accountId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT accountpassword FROM dbo.hm_accounts WHERE accountid = @AccountID;",
            connection);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        var value = await command.ExecuteScalarAsync().ConfigureAwait(false);
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task<(string Password, int PasswordEncryption)> ReadPasswordAndEncryptionAsync(
        string connectionString,
        int accountId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT accountpassword, accountpwencryption FROM dbo.hm_accounts WHERE accountid = @AccountID;",
            connection);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        Assert.IsTrue(await reader.ReadAsync().ConfigureAwait(false));
        return (
            reader.GetString(0),
            Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture));
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
