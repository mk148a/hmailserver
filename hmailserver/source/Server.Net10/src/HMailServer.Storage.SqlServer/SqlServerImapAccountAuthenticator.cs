using System.Data;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerImapAccountAuthenticator : IImapAccountAuthenticator
{
    public const string AccountLookupSql = """
SELECT TOP (1)
    a.accountid,
    a.accountaddress,
    a.accountpassword,
    a.accountpwencryption,
    a.accountisad
FROM hm_accounts AS a
INNER JOIN hm_domains AS d
    ON d.domainid = a.accountdomainid
WHERE
    LOWER(a.accountaddress) = LOWER(@Username)
    AND a.accountactive <> 0
    AND d.domainactive <> 0;
""";

    private const string UpdateLastLogonSql = """
UPDATE hm_accounts
SET accountlastlogontime = SYSUTCDATETIME()
WHERE accountid = @AccountId;
""";

    private const string InvalidUserNameOrPassword = "Invalid user name or password.";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerImapAccountAuthenticator(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<ImapAuthenticationResult> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return ImapAuthenticationResult.Failure(InvalidUserNameOrPassword);
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(AccountLookupSql, connection);
        command.Parameters.Add("@Username", SqlDbType.NVarChar, 255).Value = username.Trim();

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ImapAuthenticationResult.Failure(InvalidUserNameOrPassword);
        }

        var accountId = reader.GetInt32(0);
        var accountAddress = reader.GetString(1);
        var storedPassword = reader.GetString(2);
        var encryptionType = (LegacyPasswordEncryptionType)reader.GetByte(3);
        var isActiveDirectoryAccount = reader.GetInt32(4) != 0;

        if (isActiveDirectoryAccount)
        {
            return ImapAuthenticationResult.Failure(InvalidUserNameOrPassword);
        }

        if (!LegacyPasswordVerifier.Verify(password, storedPassword, encryptionType))
        {
            return ImapAuthenticationResult.Failure(InvalidUserNameOrPassword);
        }

        await reader.DisposeAsync().ConfigureAwait(false);
        await UpdateLastLogonAsync(connection, accountId, cancellationToken).ConfigureAwait(false);

        return ImapAuthenticationResult.Success(new ImapAuthenticatedAccount(accountId, accountAddress));
    }

    private static async ValueTask UpdateLastLogonAsync(
        SqlConnection connection,
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(UpdateLastLogonSql, connection);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
