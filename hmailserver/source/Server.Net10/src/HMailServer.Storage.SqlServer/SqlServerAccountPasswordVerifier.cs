using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerAccountPasswordVerifier
{
    public const string AccountPasswordLookupSql = """
        SELECT TOP (1)
            accountid,
            accountactive,
            accountisad,
            accountpassword,
            accountpwencryption
        FROM hm_accounts
        WHERE accountid = @AccountID;
        """;

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly IAccountAdministrationStore? _accountStore;
    private readonly IClientPasswordValidationScriptExecutor? _scriptExecutor;
    private readonly IActiveDirectoryPasswordValidator _activeDirectoryPasswordValidator;

    public SqlServerAccountPasswordVerifier(
        SqlServerConnectionFactory connectionFactory,
        IAccountAdministrationStore? accountStore = null,
        IClientPasswordValidationScriptExecutor? scriptExecutor = null,
        IActiveDirectoryPasswordValidator? activeDirectoryPasswordValidator = null)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
        _accountStore = accountStore;
        _scriptExecutor = scriptExecutor;
        _activeDirectoryPasswordValidator = activeDirectoryPasswordValidator
            ?? new WindowsActiveDirectoryPasswordValidator();
    }

    public bool Verify(int accountId, string? password)
    {
        if (accountId <= 0)
        {
            return false;
        }

        try
        {
            using var connection = _connectionFactory
                .OpenAsync(CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            using var command = new SqlCommand(AccountPasswordLookupSql, connection);
            command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
            using var reader = command.ExecuteReader(CommandBehavior.SingleRow);

            if (!reader.Read()
                || reader.IsDBNull(0)
                || reader.IsDBNull(1)
                || reader.IsDBNull(2)
                || reader.IsDBNull(3)
                || reader.IsDBNull(4))
            {
                return false;
            }

            var storedAccountId = reader.GetInt32(0);
            var accountIsAd = Convert.ToInt32(reader.GetValue(2));
            var storedPassword = reader.GetString(3);
            var encryptionType = (LegacyPasswordEncryptionType)reader.GetByte(4);

            if (storedAccountId != accountId)
            {
                return false;
            }

            var account = _accountStore?
                .GetAccountByIdAsync(accountId, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            var scriptDecision = RunScript(account, accountIsAd != 0, storedPassword, password);
            if (scriptDecision == ClientPasswordValidationScriptDecision.Accept)
            {
                return true;
            }

            if (scriptDecision == ClientPasswordValidationScriptDecision.Reject
                || string.IsNullOrEmpty(password))
            {
                return false;
            }

            if (accountIsAd != 0)
            {
                return account is not null
                    && _activeDirectoryPasswordValidator.Validate(
                        account.ActiveDirectoryDomain,
                        account.ActiveDirectoryUsername,
                        password);
            }

            return LegacyPasswordVerifier.Verify(password, storedPassword, encryptionType);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private ClientPasswordValidationScriptDecision RunScript(
        AccountAdministrationSnapshot? account,
        bool isActiveDirectoryAccount,
        string storedPassword,
        string? password)
    {
        if (_scriptExecutor is null || account is null || password is null)
        {
            return ClientPasswordValidationScriptDecision.Continue;
        }

        try
        {
            return _scriptExecutor.Execute(
                new ClientPasswordValidationScriptRequest(
                    new ScriptAccount(
                        account.Id,
                        account.Address,
                        storedPassword,
                        account.Active,
                        isActiveDirectoryAccount,
                        account.DomainId,
                        account.ActiveDirectoryDomain,
                        account.ActiveDirectoryUsername,
                        account.MaxSize,
                        account.PersonFirstName,
                        account.PersonLastName,
                        account.AdminLevel,
                        account.VacationMessageIsOn,
                        account.VacationMessage,
                        account.VacationSubject,
                        account.VacationMessageExpires,
                        account.VacationMessageExpiresDate,
                        account.VacationMessageAbortSpamFlagged,
                        account.ForwardEnabled,
                        account.ForwardAddress,
                        account.ForwardKeepOriginal,
                        account.ForwardAbortSpamFlagged,
                        account.SignatureEnabled,
                        account.SignaturePlainText,
                        account.SignatureHtml,
                        account.LastLogonTime == default
                            ? string.Empty
                            : account.LastLogonTime.ToString("O", CultureInfo.InvariantCulture)),
                    password),
                CancellationToken.None).Decision;
        }
        catch
        {
            return ClientPasswordValidationScriptDecision.Continue;
        }
    }
}
