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
    CONVERT(int, a.accountisad) AS accountisad,
    CONVERT(int, a.accountactive) AS accountactive,
    a.accountdomainid,
    a.accountaddomain,
    a.accountadusername,
    a.accountmaxsize,
    a.accountpersonfirstname,
    a.accountpersonlastname,
    CONVERT(int, a.accountadminlevel) AS accountadminlevel,
    CONVERT(int, a.accountvacationmessageon) AS accountvacationmessageon,
    a.accountvacationmessage,
    a.accountvacationsubject,
    CONVERT(int, a.accountvacationexpires) AS accountvacationexpires,
    CONVERT(varchar(10), a.accountvacationexpiredate, 23) AS accountvacationexpiredate,
    CONVERT(int, a.accountvacationabortspamflagged) AS accountvacationabortspamflagged,
    CONVERT(int, a.accountforwardenabled) AS accountforwardenabled,
    a.accountforwardaddress,
    CONVERT(int, a.accountforwardkeeporiginal) AS accountforwardkeeporiginal,
    CONVERT(int, a.accountforwardabortspamflagged) AS accountforwardabortspamflagged,
    CONVERT(int, a.accountenablesignature) AS accountenablesignature,
    a.accountsignatureplaintext,
    a.accountsignaturehtml,
    CONVERT(varchar(30), a.accountlastlogontime, 126) AS accountlastlogontime
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

    private const string TargetLookupSql = """
SELECT TOP (1)
    a.accountid,
    a.accountaddress
FROM hm_accounts AS a
INNER JOIN hm_domains AS d
    ON d.domainid = a.accountdomainid
WHERE
    LOWER(a.accountaddress) = LOWER(@Username)
    AND a.accountactive <> 0
    AND d.domainactive <> 0;
""";

    private const string InvalidUserNameOrPassword = "Invalid user name or password.";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly IClientPasswordValidationScriptExecutor? _passwordValidationScriptExecutor;
    private readonly ISettingsAdministrationStore? _settingsAdministrationStore;
    private readonly IActiveDirectoryPasswordValidator _activeDirectoryPasswordValidator;

    public SqlServerImapAccountAuthenticator(
        SqlServerConnectionFactory connectionFactory,
        IClientPasswordValidationScriptExecutor? passwordValidationScriptExecutor = null,
        ISettingsAdministrationStore? settingsAdministrationStore = null,
        IActiveDirectoryPasswordValidator? activeDirectoryPasswordValidator = null)
    {
        _connectionFactory = connectionFactory;
        _passwordValidationScriptExecutor = passwordValidationScriptExecutor;
        _settingsAdministrationStore = settingsAdministrationStore;
        _activeDirectoryPasswordValidator = activeDirectoryPasswordValidator
            ?? new WindowsActiveDirectoryPasswordValidator();
    }

    public ValueTask<ImapAuthenticationResult> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
        => AuthenticateAsync(username, password, string.Empty, cancellationToken);

    public async ValueTask<ImapAuthenticationResult> AuthenticateAsync(
        string username,
        string password,
        string authorizationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(authorizationId))
        {
            return await AuthenticateNormalAsync(username, password, cancellationToken)
                .ConfigureAwait(false);
        }

        if (_settingsAdministrationStore is null)
        {
            return ImapAuthenticationResult.Failure("No master user defined.", isProtocolError: true);
        }

        var settings = await _settingsAdministrationStore
            .GetSettingsAsync(cancellationToken)
            .ConfigureAwait(false);
        if ((!username.Contains('@', StringComparison.Ordinal)
                || !authorizationId.Contains('@', StringComparison.Ordinal))
            && string.IsNullOrEmpty(settings.DefaultDomain))
        {
            return ImapAuthenticationResult.Failure(
                "Invalid user name. Please use full email address as user name.",
                isProtocolError: true);
        }

        var authenticationId = Canonicalize(username, settings.DefaultDomain);
        var authorizationAddress = Canonicalize(authorizationId, settings.DefaultDomain);
        var masterUser = settings.ImapMasterUser;
        if (string.IsNullOrEmpty(masterUser))
        {
            return ImapAuthenticationResult.Failure("No master user defined.", isProtocolError: true);
        }

        if (authorizationId.Contains('@', StringComparison.Ordinal))
        {
            masterUser += "@" + ExtractDomain(authenticationId);
        }
        else
        {
            masterUser = Canonicalize(masterUser, settings.DefaultDomain);
        }

        if (!masterUser.Equals(authenticationId, StringComparison.Ordinal))
        {
            return ImapAuthenticationResult.Failure("Invalid master user.", isProtocolError: true);
        }

        var masterAuthentication = await AuthenticateNormalAsync(
                authenticationId,
                password,
                cancellationToken)
            .ConfigureAwait(false);
        if (!masterAuthentication.Succeeded)
        {
            return masterAuthentication;
        }

        int targetAccountId;
        string targetAddress;
        await using (var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            await using var command = new SqlCommand(TargetLookupSql, connection);
            command.Parameters.Add("@Username", SqlDbType.NVarChar, 255).Value = authorizationAddress;
            await using var reader = await command
                .ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken)
                .ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return ImapAuthenticationResult.Failure(InvalidUserNameOrPassword);
            }

            targetAccountId = reader.GetInt32(0);
            targetAddress = reader.GetString(1);
        }

        await UpdateLastLogonAsync(targetAccountId, cancellationToken)
            .ConfigureAwait(false);
        return ImapAuthenticationResult.Success(
            new ImapAuthenticatedAccount(targetAccountId, targetAddress));
    }

    private async ValueTask<ImapAuthenticationResult> AuthenticateNormalAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return ImapAuthenticationResult.Failure(InvalidUserNameOrPassword);
        }

        int accountId;
        string accountAddress;
        string storedPassword;
        LegacyPasswordEncryptionType encryptionType;
        bool isActiveDirectoryAccount;
        string activeDirectoryDomain;
        string activeDirectoryUsername;
        ScriptAccount account;
        await using (var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            await using var command = new SqlCommand(AccountLookupSql, connection);
            command.Parameters.Add("@Username", SqlDbType.NVarChar, 255).Value = username.Trim();

            await using var reader = await command
                .ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken)
                .ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return ImapAuthenticationResult.Failure(InvalidUserNameOrPassword);
            }

            accountId = reader.GetInt32(0);
            accountAddress = reader.GetString(1);
            storedPassword = reader.GetString(2);
            encryptionType = (LegacyPasswordEncryptionType)reader.GetByte(3);
            isActiveDirectoryAccount = reader.GetInt32(4) != 0;
            activeDirectoryDomain = GetStringOrEmpty(reader, 7);
            activeDirectoryUsername = GetStringOrEmpty(reader, 8);
            account = new ScriptAccount(
                accountId,
                accountAddress,
                Password: storedPassword,
                Active: reader.GetInt32(5) != 0,
                isActiveDirectoryAccount,
                DomainId: reader.GetInt32(6),
                ActiveDirectoryDomain: activeDirectoryDomain,
                ActiveDirectoryUsername: activeDirectoryUsername,
                MaxSizeMegabytes: reader.GetInt32(9),
                PersonFirstName: GetStringOrEmpty(reader, 10),
                PersonLastName: GetStringOrEmpty(reader, 11),
                AdminLevel: reader.GetInt32(12),
                VacationMessageIsOn: reader.GetInt32(13) != 0,
                VacationMessage: GetStringOrEmpty(reader, 14),
                VacationSubject: GetStringOrEmpty(reader, 15),
                VacationMessageExpires: reader.GetInt32(16) != 0,
                VacationMessageExpiresDate: GetStringOrEmpty(reader, 17),
                VacationMessageAbortSpamFlagged: reader.GetInt32(18) != 0,
                ForwardEnabled: reader.GetInt32(19) != 0,
                ForwardAddress: GetStringOrEmpty(reader, 20),
                ForwardKeepOriginal: reader.GetInt32(21) != 0,
                ForwardAbortSpamFlagged: reader.GetInt32(22) != 0,
                SignatureEnabled: reader.GetInt32(23) != 0,
                SignaturePlainText: GetStringOrEmpty(reader, 24),
                SignatureHtml: GetStringOrEmpty(reader, 25),
                LastLogonTime: GetStringOrEmpty(reader, 26));
        }

        var scriptDecision = RunPasswordValidationScript(account, password, cancellationToken);
        if (scriptDecision == ClientPasswordValidationScriptDecision.Accept)
        {
            await UpdateLastLogonAsync(accountId, cancellationToken).ConfigureAwait(false);
            return ImapAuthenticationResult.Success(new ImapAuthenticatedAccount(accountId, accountAddress));
        }

        if (scriptDecision == ClientPasswordValidationScriptDecision.Reject)
        {
            return ImapAuthenticationResult.Failure(InvalidUserNameOrPassword);
        }

        if (string.IsNullOrEmpty(password))
        {
            return ImapAuthenticationResult.Failure(InvalidUserNameOrPassword);
        }

        if (isActiveDirectoryAccount)
        {
            bool validated;
            try
            {
                validated = _activeDirectoryPasswordValidator.Validate(
                    activeDirectoryDomain,
                    activeDirectoryUsername,
                    password);
            }
            catch
            {
                validated = false;
            }
            if (!validated)
            {
                return ImapAuthenticationResult.Failure(InvalidUserNameOrPassword);
            }

            await UpdateLastLogonAsync(accountId, cancellationToken)
                .ConfigureAwait(false);
            return ImapAuthenticationResult.Success(
                new ImapAuthenticatedAccount(accountId, accountAddress));
        }

        if (!LegacyPasswordVerifier.Verify(password, storedPassword, encryptionType))
        {
            return ImapAuthenticationResult.Failure(InvalidUserNameOrPassword);
        }

        await UpdateLastLogonAsync(accountId, cancellationToken).ConfigureAwait(false);

        return ImapAuthenticationResult.Success(new ImapAuthenticatedAccount(accountId, accountAddress));
    }

    private ClientPasswordValidationScriptDecision RunPasswordValidationScript(
        ScriptAccount account,
        string password,
        CancellationToken cancellationToken)
    {
        if (_passwordValidationScriptExecutor is null)
        {
            return ClientPasswordValidationScriptDecision.Continue;
        }

        try
        {
            return _passwordValidationScriptExecutor.Execute(
                new ClientPasswordValidationScriptRequest(account, password),
                cancellationToken).Decision;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return ClientPasswordValidationScriptDecision.Continue;
        }
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

    private async ValueTask UpdateLastLogonAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await UpdateLastLogonAsync(connection, accountId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string Canonicalize(string value, string defaultDomain)
    {
        var trimmed = value.Trim();
        if (trimmed.Contains('@', StringComparison.Ordinal) || string.IsNullOrEmpty(defaultDomain))
        {
            return trimmed;
        }

        return trimmed + "@" + defaultDomain;
    }

    private static string ExtractDomain(string value)
    {
        var at = value.IndexOf('@');
        return at >= 0 && at + 1 < value.Length
            ? value[(at + 1)..]
            : string.Empty;
    }

    private static string GetStringOrEmpty(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
}
