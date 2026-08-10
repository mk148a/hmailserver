using System.Data;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerAccountPasswordVerifierTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    public void AccountPasswordLookupSql_IsParameterizedReadOnlyAndUsesRequiredColumns()
    {
        var sql = SqlServerAccountPasswordVerifier.AccountPasswordLookupSql;

        foreach (var column in new[]
        {
            "accountid", "accountactive", "accountisad", "accountpassword", "accountpwencryption"
        })
        {
            StringAssert.Contains(sql, column);
        }

        StringAssert.Contains(sql, "@AccountID");
        StringAssert.Contains(sql, "FROM hm_accounts");
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Verify_SqlFailureFailsClosed()
    {
        var verifier = new SqlServerAccountPasswordVerifier(
            new SqlServerConnectionFactory(
                "Server=invalid;Database=unused;Integrated Security=true;Connect Timeout=1;TrustServerCertificate=true"));

        Assert.IsFalse(verifier.Verify(1, "candidate"));
    }

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task Verify_NormalAccountsRejectsWrongInactiveMissingAdAndInvalidRows()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_password_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var verifier = new SqlServerAccountPasswordVerifier(
                new SqlServerConnectionFactory(testConnectionString));

            Assert.IsTrue(verifier.Verify(1, "secret"));
            Assert.IsFalse(verifier.Verify(1, "wrong"));
            Assert.IsTrue(verifier.Verify(2, "secret"));
            Assert.IsFalse(verifier.Verify(3, "secret"));
            Assert.IsFalse(verifier.Verify(4, "secret"));
            Assert.IsFalse(verifier.Verify(5, "secret"));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    [TestMethod]
    public void ValidatePassword_DirectActivationStillRequiresAttachment()
    {
        var error = Assert.ThrowsExactly<System.Runtime.InteropServices.COMException>(
            () => new Account().ValidatePassword("candidate"));

        Assert.AreEqual(unchecked((int)0x80070005), error.ErrorCode);
    }

    private static string GetApprovedConnectionStringOrInconclusive()
    {
        var raw = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        var allowCreate = Environment.GetEnvironmentVariable(AllowDatabaseCreateEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(raw)
            || !string.Equals(allowCreate, "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive(
                $"Set {ConnectionEnvironmentVariable} to a disposable local SQL target and " +
                $"{AllowDatabaseCreateEnvironmentVariable}=1 to run this destructive fixture.");
        }

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(raw);
        }
        catch (Exception)
        {
            Assert.Inconclusive("The SQL integration connection string is invalid.");
            throw;
        }

        var dataSource = builder.DataSource.Trim();
        if (!dataSource.Equals(".", StringComparison.OrdinalIgnoreCase)
            && !dataSource.Equals("(local)", StringComparison.OrdinalIgnoreCase)
            && !dataSource.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            && !dataSource.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            && !dataSource.StartsWith(".\\", StringComparison.OrdinalIgnoreCase)
            && !dataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase)
            && !dataSource.StartsWith("localhost\\", StringComparison.OrdinalIgnoreCase)
            && !dataSource.StartsWith("127.0.0.1,", StringComparison.OrdinalIgnoreCase)
            && !dataSource.StartsWith("localhost,", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Inconclusive("The SQL integration fixture only accepts a local SQL/LocalDB target.");
        }

        return builder.ConnectionString;
    }

    private static string WithDatabase(string connectionString, string databaseName) =>
        new SqlConnectionStringBuilder(connectionString) { InitialCatalog = databaseName }.ConnectionString;

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
                accountid int NOT NULL PRIMARY KEY,
                accountactive tinyint NULL,
                accountisad tinyint NULL,
                accountpassword nvarchar(255) NULL,
                accountpwencryption tinyint NULL
            );
            INSERT INTO dbo.hm_accounts (accountid, accountactive, accountisad, accountpassword, accountpwencryption)
            VALUES
                (1, 1, 0, N'secret', 0),
                (2, 0, 0, N'secret', 0),
                (3, 1, 1, N'secret', 0),
                (4, 1, 0, NULL, 0),
                (5, 1, 0, N'secret', 99);
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
