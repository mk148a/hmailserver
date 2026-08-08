using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerAccountAdministrationStore :
    IAccountAdministrationStore,
    IBackupAccountAdministrationStore
{
    public const string GetAccountsSql = """
SELECT
    accountid,
    accountdomainid,
    accountaddress,
    accountactive,
    accountadminlevel,
    accountisad,
    accountaddomain,
    accountadusername,
    accountmaxsize,
    (
        SELECT COALESCE(SUM(CAST(messagesize AS bigint)), 0)
        FROM hm_messages
        WHERE messageaccountid = hm_accounts.accountid
    ) AS accountsizebytes,
    accountlastlogontime,
    accountpersonfirstname,
    accountpersonlastname,
    accountvacationmessageon,
    accountvacationmessage,
    accountvacationsubject,
    accountvacationexpires,
    CONVERT(varchar(10), accountvacationexpiredate, 23) AS accountvacationexpiredate,
    accountvacationabortspamflagged,
    accountforwardenabled,
    accountforwardaddress,
    accountforwardkeeporiginal,
    accountforwardabortspamflagged,
    accountenablesignature,
    CONVERT(nvarchar(max), accountsignatureplaintext) AS accountsignatureplaintext,
    CONVERT(nvarchar(max), accountsignaturehtml) AS accountsignaturehtml
FROM hm_accounts
WHERE accountdomainid = @DomainID
ORDER BY accountaddress ASC;
""";

    public const string GetAccountByIdSql = """
SELECT
    accountid,
    accountdomainid,
    accountaddress,
    accountactive,
    accountadminlevel,
    accountisad,
    accountaddomain,
    accountadusername,
    accountmaxsize,
    (
        SELECT COALESCE(SUM(CAST(messagesize AS bigint)), 0)
        FROM hm_messages
        WHERE messageaccountid = hm_accounts.accountid
    ) AS accountsizebytes,
    accountlastlogontime,
    accountpersonfirstname,
    accountpersonlastname,
    accountvacationmessageon,
    accountvacationmessage,
    accountvacationsubject,
    accountvacationexpires,
    CONVERT(varchar(10), accountvacationexpiredate, 23) AS accountvacationexpiredate,
    accountvacationabortspamflagged,
    accountforwardenabled,
    accountforwardaddress,
    accountforwardkeeporiginal,
    accountforwardabortspamflagged,
    accountenablesignature,
    CONVERT(nvarchar(max), accountsignatureplaintext) AS accountsignatureplaintext,
    CONVERT(nvarchar(max), accountsignaturehtml) AS accountsignaturehtml
FROM hm_accounts
WHERE accountid = @AccountID;
""";

    public const string GetBackupAccountsSql = """
SELECT
    accountid,
    accountdomainid,
    accountaddress,
    accountactive,
    accountpassword,
    accountpwencryption,
    accountadminlevel,
    accountisad,
    accountaddomain,
    accountadusername,
    accountmaxsize,
    (
        SELECT COALESCE(SUM(CAST(messagesize AS bigint)), 0)
        FROM hm_messages
        WHERE messageaccountid = hm_accounts.accountid
    ) AS accountsizebytes,
    accountlastlogontime,
    accountpersonfirstname,
    accountpersonlastname,
    accountvacationmessageon,
    accountvacationmessage,
    accountvacationsubject,
    accountvacationexpires,
    CONVERT(varchar(10), accountvacationexpiredate, 23) AS accountvacationexpiredate,
    accountvacationabortspamflagged,
    accountforwardenabled,
    accountforwardaddress,
    accountforwardkeeporiginal,
    accountforwardabortspamflagged,
    accountenablesignature,
    CONVERT(nvarchar(max), accountsignatureplaintext) AS accountsignatureplaintext,
    CONVERT(nvarchar(max), accountsignaturehtml) AS accountsignaturehtml
FROM hm_accounts
WHERE accountdomainid = @DomainID
ORDER BY accountaddress ASC;
""";

    public const string InsertAccountSql = """
        INSERT INTO hm_accounts
            (accountdomainid, accountaddress, accountpassword, accountactive, accountisad,
             accountaddomain, accountadusername, accountmaxsize, accountvacationmessageon,
             accountvacationmessage, accountvacationsubject, accountvacationexpires,
             accountvacationexpiredate, accountvacationabortspamflagged, accountpwencryption,
             accountadminlevel, accountforwardenabled, accountforwardaddress,
             accountforwardkeeporiginal, accountforwardabortspamflagged, accountenablesignature,
             accountsignatureplaintext, accountsignaturehtml, accountlastlogontime,
             accountpersonfirstname, accountpersonlastname)
        OUTPUT INSERTED.accountid
        VALUES
            (@DomainID, @Address, @Password, @Active, @IsAD,
             @ADDomain, @ADUsername, @MaxSize, @VacationMessageIsOn,
             @VacationMessage, @VacationSubject, @VacationExpires,
             @VacationExpiresDate, @VacationAbortSpamFlagged, @PasswordEncryption,
             @AdminLevel, @ForwardEnabled, @ForwardAddress,
             @ForwardKeepOriginal, @ForwardAbortSpamFlagged, @SignatureEnabled,
             @SignaturePlainText, @SignatureHtml, @LastLogonTime,
             @PersonFirstName, @PersonLastName);
        """;    public const string DeleteAccountSql = """
        SET XACT_ABORT ON;
        BEGIN TRANSACTION;

        DECLARE @Deleted bit = 0;
        IF EXISTS (SELECT 1 FROM hm_accounts WITH (UPDLOCK, HOLDLOCK) WHERE accountid = @AccountID AND accountdomainid = @DomainID)
        BEGIN
            DELETE FROM hm_rule_criterias
                WHERE criteriaruleid IN (SELECT ruleid FROM hm_rules WHERE ruleaccountid = @AccountID);
            DELETE FROM hm_rule_actions
                WHERE actionruleid IN (SELECT ruleid FROM hm_rules WHERE ruleaccountid = @AccountID);
            DELETE FROM hm_rules WHERE ruleaccountid = @AccountID;
            DELETE FROM hm_messagerecipients
                WHERE recipientmessageid IN (SELECT messageid FROM hm_messages WHERE messageaccountid = @AccountID);
            DELETE FROM hm_message_metadata WHERE metadata_accountid = @AccountID;
            DELETE FROM hm_message_search_queue
                WHERE messageid IN (SELECT messageid FROM hm_messages WHERE messageaccountid = @AccountID);
            DELETE FROM hm_message_search_documents
                WHERE messageid IN (SELECT messageid FROM hm_messages WHERE messageaccountid = @AccountID);
            DELETE FROM hm_messages WHERE messageaccountid = @AccountID;
            DELETE FROM hm_fetchaccounts WHERE faaccountid = @AccountID;
            DELETE FROM hm_accounts WHERE accountid = @AccountID AND accountdomainid = @DomainID;
            IF @@ROWCOUNT = 1 SET @Deleted = 1;
        END;

        IF @Deleted = 1 COMMIT TRANSACTION; ELSE ROLLBACK TRANSACTION;
        SELECT @Deleted;
        """;    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly SqlServerBackupRestoreTransactionContext? _transactionContext;

    public SqlServerAccountAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    internal SqlServerAccountAdministrationStore(
        SqlServerBackupRestoreTransactionContext transactionContext)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        _connectionFactory = null!;
        _transactionContext = transactionContext;
    }

    private SqlServerConnectionFactory GetStandaloneConnectionFactory() =>
        _transactionContext is not null
            ? throw new InvalidOperationException(
                "Non-transaction-aware operations are not supported on transaction-scoped SQL administration stores.")
            : _connectionFactory;

    public async ValueTask<bool> UpdateAccountAsync(
        int domainId,
        AccountAdministrationSnapshot account,
        string? password,
        CancellationToken cancellationToken)
    {
        var connectionFactory = GetStandaloneConnectionFactory();
        ArgumentNullException.ThrowIfNull(account);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(BuildUpdateAccountSql(password != null), connection);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = account.Id;
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = domainId;
        command.Parameters.Add("@Address", SqlDbType.NVarChar, 255).Value = account.Address;
        command.Parameters.Add("@Active", SqlDbType.TinyInt).Value = account.Active ? 1 : 0;
        command.Parameters.Add("@IsActive", SqlDbType.TinyInt).Value = account.IsActiveDirectoryAccount ? 1 : 0;
        command.Parameters.Add("@ADDomain", SqlDbType.NVarChar, 255).Value = account.ActiveDirectoryDomain;
        command.Parameters.Add("@ADUsername", SqlDbType.NVarChar, 255).Value = account.ActiveDirectoryUsername;
        command.Parameters.Add("@MaxSize", SqlDbType.Int).Value = account.MaxSize;
        command.Parameters.Add("@VacationMessageIsOn", SqlDbType.TinyInt).Value = account.VacationMessageIsOn ? 1 : 0;
        command.Parameters.Add("@VacationMessage", SqlDbType.NVarChar, 1000).Value = account.VacationMessage;
        command.Parameters.Add("@VacationSubject", SqlDbType.NVarChar, 200).Value = account.VacationSubject;
        command.Parameters.Add("@VacationExpires", SqlDbType.TinyInt).Value = account.VacationMessageExpires ? 1 : 0;
        command.Parameters.Add("@VacationExpiresDate", SqlDbType.NVarChar, 255).Value = account.VacationMessageExpiresDate;
        command.Parameters.Add("@VacationAbortSpamFlagged", SqlDbType.TinyInt).Value = account.VacationMessageAbortSpamFlagged ? 1 : 0;
        command.Parameters.Add("@AdminLevel", SqlDbType.TinyInt).Value = account.AdminLevel;
        command.Parameters.Add("@ForwardEnabled", SqlDbType.TinyInt).Value = account.ForwardEnabled ? 1 : 0;
        command.Parameters.Add("@ForwardAddress", SqlDbType.NVarChar, 255).Value = account.ForwardAddress;
        command.Parameters.Add("@ForwardKeepOriginal", SqlDbType.TinyInt).Value = account.ForwardKeepOriginal ? 1 : 0;
        command.Parameters.Add("@ForwardAbortSpamFlagged", SqlDbType.TinyInt).Value = account.ForwardAbortSpamFlagged ? 1 : 0;
        command.Parameters.Add("@SignatureEnabled", SqlDbType.TinyInt).Value = account.SignatureEnabled ? 1 : 0;
        command.Parameters.Add("@SignaturePlainText", SqlDbType.NVarChar, -1).Value = account.SignaturePlainText;
        command.Parameters.Add("@SignatureHtml", SqlDbType.NVarChar, -1).Value = account.SignatureHtml;
        command.Parameters.Add("@LastLogonTime", SqlDbType.DateTime).Value = account.LastLogonTime;
        command.Parameters.Add("@PersonFirstName", SqlDbType.NVarChar, 60).Value = account.PersonFirstName;
        command.Parameters.Add("@PersonLastName", SqlDbType.NVarChar, 60).Value = account.PersonLastName;
        if (password is not null)
        {
            command.Parameters.Add("@Password", SqlDbType.NVarChar, 255).Value =
                LegacyBlowfishPasswordCipher.Encrypt(password);
            command.Parameters.Add("@PasswordEncryption", SqlDbType.TinyInt).Value = 1;
        }

        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected == 1;
    }

    private static string BuildUpdateAccountSql(bool includePassword) =>
        includePassword ? UpdateAccountWithPasswordSql : UpdateAccountSql;

    public const string UpdateAccountSql = """
        UPDATE hm_accounts
        SET accountaddress = @Address,
            accountactive = @Active,
            accountisad = @IsActive,
            accountaddomain = @ADDomain,
            accountadusername = @ADUsername,
            accountmaxsize = @MaxSize,
            accountvacationmessageon = @VacationMessageIsOn,
            accountvacationmessage = @VacationMessage,
            accountvacationsubject = @VacationSubject,
            accountvacationexpires = @VacationExpires,
            accountvacationexpiredate = @VacationExpiresDate,
            accountvacationabortspamflagged = @VacationAbortSpamFlagged,
            accountadminlevel = @AdminLevel,
            accountforwardenabled = @ForwardEnabled,
            accountforwardaddress = @ForwardAddress,
            accountforwardkeeporiginal = @ForwardKeepOriginal,
            accountforwardabortspamflagged = @ForwardAbortSpamFlagged,
            accountenablesignature = @SignatureEnabled,
            accountsignatureplaintext = @SignaturePlainText,
            accountsignaturehtml = @SignatureHtml,
            accountlastlogontime = @LastLogonTime,
            accountpersonfirstname = @PersonFirstName,
            accountpersonlastname = @PersonLastName
        WHERE accountid = @AccountID AND accountdomainid = @DomainID;
        """;

    public const string UpdateAccountWithPasswordSql = """
        UPDATE hm_accounts
        SET accountaddress = @Address,
            accountpassword = @Password,
            accountactive = @Active,
            accountisad = @IsActive,
            accountaddomain = @ADDomain,
            accountadusername = @ADUsername,
            accountmaxsize = @MaxSize,
            accountvacationmessageon = @VacationMessageIsOn,
            accountvacationmessage = @VacationMessage,
            accountvacationsubject = @VacationSubject,
            accountvacationexpires = @VacationExpires,
            accountvacationexpiredate = @VacationExpiresDate,
            accountvacationabortspamflagged = @VacationAbortSpamFlagged,
            accountpwencryption = @PasswordEncryption,
            accountadminlevel = @AdminLevel,
            accountforwardenabled = @ForwardEnabled,
            accountforwardaddress = @ForwardAddress,
            accountforwardkeeporiginal = @ForwardKeepOriginal,
            accountforwardabortspamflagged = @ForwardAbortSpamFlagged,
            accountenablesignature = @SignatureEnabled,
            accountsignatureplaintext = @SignaturePlainText,
            accountsignaturehtml = @SignatureHtml,
            accountlastlogontime = @LastLogonTime,
            accountpersonfirstname = @PersonFirstName,
            accountpersonlastname = @PersonLastName
        WHERE accountid = @AccountID AND accountdomainid = @DomainID;
        """;    public async ValueTask<bool> DeleteAccountAsync(
        int domainId,
        int accountId,
        CancellationToken cancellationToken)
    {
        var connectionFactory = GetStandaloneConnectionFactory();
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteAccountSql, connection);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = domainId;
        var deleted = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return deleted is not null && Convert.ToInt32(deleted, CultureInfo.InvariantCulture) != 0;
    }
    public ValueTask<int> InsertAccountAsync(
        int domainId,
        AccountAdministrationSnapshot account,
        string password,
        CancellationToken cancellationToken)
    {
        return InsertAccountCoreAsync(
            domainId,
            account,
            LegacyBlowfishPasswordCipher.Encrypt(password),
            passwordEncryption: 1,
            cancellationToken);
    }

    public ValueTask<int> InsertAccountForRestoreAsync(
        int domainId,
        AccountAdministrationSnapshot account,
        string password,
        int passwordEncryption,
        CancellationToken cancellationToken)
    {
        return InsertAccountCoreAsync(
            domainId,
            account,
            password,
            passwordEncryption,
            cancellationToken);
    }

    private async ValueTask<int> InsertAccountCoreAsync(
        int domainId,
        AccountAdministrationSnapshot account,
        string storedPassword,
        int passwordEncryption,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        await using var commandLease = await SqlServerCommandLease
            .OpenAsync(_connectionFactory, _transactionContext, InsertAccountSql, cancellationToken)
            .ConfigureAwait(false);
        var command = commandLease.Command;
        command.Parameters.Add("@DomainId", SqlDbType.Int).Value = domainId;
        command.Parameters.Add("@Address", SqlDbType.NVarChar, 255).Value = account.Address;
        command.Parameters.Add("@Password", SqlDbType.NVarChar, 255).Value = storedPassword;
        command.Parameters.Add("@Active", SqlDbType.TinyInt).Value = account.Active ? 1 : 0;
        command.Parameters.Add("@IsAD", SqlDbType.TinyInt).Value = account.IsActiveDirectoryAccount ? 1 : 0;
        command.Parameters.Add("@ADDomain", SqlDbType.NVarChar, 255).Value = account.ActiveDirectoryDomain;
        command.Parameters.Add("@ADUsername", SqlDbType.NVarChar, 255).Value = account.ActiveDirectoryUsername;
        command.Parameters.Add("@MaxSize", SqlDbType.Int).Value = account.MaxSize;
        command.Parameters.Add("@VacationMessageIsOn", SqlDbType.TinyInt).Value = account.VacationMessageIsOn ? 1 : 0;
        command.Parameters.Add("@VacationMessage", SqlDbType.NVarChar, 1000).Value = account.VacationMessage;
        command.Parameters.Add("@VacationSubject", SqlDbType.NVarChar, 200).Value = account.VacationSubject;
        command.Parameters.Add("@VacationExpires", SqlDbType.TinyInt).Value = account.VacationMessageExpires ? 1 : 0;
        command.Parameters.Add("@VacationExpiresDate", SqlDbType.NVarChar, 255).Value = account.VacationMessageExpiresDate;
        command.Parameters.Add("@VacationAbortSpamFlagged", SqlDbType.TinyInt).Value = account.VacationMessageAbortSpamFlagged ? 1 : 0;
        command.Parameters.Add("@PasswordEncryption", SqlDbType.TinyInt).Value = passwordEncryption;
        command.Parameters.Add("@AdminLevel", SqlDbType.TinyInt).Value = account.AdminLevel;
        command.Parameters.Add("@ForwardEnabled", SqlDbType.TinyInt).Value = account.ForwardEnabled ? 1 : 0;
        command.Parameters.Add("@ForwardAddress", SqlDbType.NVarChar, 255).Value = account.ForwardAddress;
        command.Parameters.Add("@ForwardKeepOriginal", SqlDbType.TinyInt).Value = account.ForwardKeepOriginal ? 1 : 0;
        command.Parameters.Add("@ForwardAbortSpamFlagged", SqlDbType.TinyInt).Value = account.ForwardAbortSpamFlagged ? 1 : 0;
        command.Parameters.Add("@SignatureEnabled", SqlDbType.TinyInt).Value = account.SignatureEnabled ? 1 : 0;
        command.Parameters.Add("@SignaturePlainText", SqlDbType.NVarChar, -1).Value = account.SignaturePlainText;
        command.Parameters.Add("@SignatureHtml", SqlDbType.NVarChar, -1).Value = account.SignatureHtml;
        command.Parameters.Add("@LastLogonTime", SqlDbType.DateTime).Value = account.LastLogonTime;
        command.Parameters.Add("@PersonFirstName", SqlDbType.NVarChar, 60).Value = account.PersonFirstName;
        command.Parameters.Add("@PersonLastName", SqlDbType.NVarChar, 60).Value = account.PersonLastName;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, CultureInfo.InvariantCulture);
    }
    public async ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> GetAccountsAsync(
        int domainId,
        CancellationToken cancellationToken)
    {
        var connectionFactory = GetStandaloneConnectionFactory();
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetAccountsSql, connection);
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = domainId;

        return await ReadAccountsAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<AccountAdministrationSnapshot?> GetAccountByIdAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        var connectionFactory = GetStandaloneConnectionFactory();
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetAccountByIdSql, connection);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;

        return (await ReadAccountsAsync(command, cancellationToken).ConfigureAwait(false)).FirstOrDefault();
    }

    public async ValueTask<IReadOnlyList<AccountBackupAdministrationSnapshot>> GetBackupAccountsAsync(
        int domainId,
        CancellationToken cancellationToken)
    {
        var connectionFactory = GetStandaloneConnectionFactory();
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetBackupAccountsSql, connection);
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = domainId;

        return await ReadBackupAccountsAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> ReadAccountsAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.Default,
            cancellationToken).ConfigureAwait(false);

        var accounts = new List<AccountAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            accounts.Add(ReadAccountSnapshot(reader, adminLevelOrdinal: 4));
        }

        return accounts;
    }

    private static async ValueTask<IReadOnlyList<AccountBackupAdministrationSnapshot>> ReadBackupAccountsAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.Default,
            cancellationToken).ConfigureAwait(false);

        var accounts = new List<AccountBackupAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            accounts.Add(
                new AccountBackupAdministrationSnapshot(
                    Account: ReadAccountSnapshot(reader, adminLevelOrdinal: 6),
                    Password: reader.GetString(4),
                    PasswordEncryption: Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture)));
        }

        return accounts;
    }

    private static AccountAdministrationSnapshot ReadAccountSnapshot(
        SqlDataReader reader,
        int adminLevelOrdinal)
    {
        var maxSize = reader.GetInt32(adminLevelOrdinal + 4);
        var sizeBytes = Convert.ToInt64(reader.GetValue(adminLevelOrdinal + 5), CultureInfo.InvariantCulture);
        return new AccountAdministrationSnapshot(
            Id: reader.GetInt32(0),
            DomainId: reader.GetInt32(1),
            Address: reader.GetString(2),
            Active: Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) != 0,
            AdminLevel: Convert.ToInt32(reader.GetValue(adminLevelOrdinal), CultureInfo.InvariantCulture),
            IsActiveDirectoryAccount: ReadLegacyBoolean(reader, adminLevelOrdinal + 1),
            ActiveDirectoryDomain: reader.GetString(adminLevelOrdinal + 2),
            ActiveDirectoryUsername: reader.GetString(adminLevelOrdinal + 3),
            MaxSize: maxSize,
            Size: CalculateLegacySizeMb(sizeBytes),
            QuotaUsed: CalculateLegacyQuotaUsed(sizeBytes, maxSize),
            LastLogonTime: reader.GetDateTime(adminLevelOrdinal + 6),
            PersonFirstName: reader.GetString(adminLevelOrdinal + 7),
            PersonLastName: reader.GetString(adminLevelOrdinal + 8),
            VacationMessageIsOn: ReadLegacyBoolean(reader, adminLevelOrdinal + 9),
            VacationMessage: reader.GetString(adminLevelOrdinal + 10),
            VacationSubject: reader.GetString(adminLevelOrdinal + 11),
            VacationMessageExpires: ReadLegacyBoolean(reader, adminLevelOrdinal + 12),
            VacationMessageExpiresDate: reader.GetString(adminLevelOrdinal + 13),
            VacationMessageAbortSpamFlagged: ReadLegacyBoolean(reader, adminLevelOrdinal + 14),
            ForwardEnabled: ReadLegacyBoolean(reader, adminLevelOrdinal + 15),
            ForwardAddress: reader.GetString(adminLevelOrdinal + 16),
            ForwardKeepOriginal: ReadLegacyBoolean(reader, adminLevelOrdinal + 17),
            ForwardAbortSpamFlagged: ReadLegacyBoolean(reader, adminLevelOrdinal + 18),
            SignatureEnabled: ReadLegacyBoolean(reader, adminLevelOrdinal + 19),
            SignaturePlainText: reader.GetString(adminLevelOrdinal + 20),
            SignatureHtml: reader.GetString(adminLevelOrdinal + 21));
    }

    private static float CalculateLegacySizeMb(long sizeBytes) =>
        MathF.Round((float)sizeBytes / (1024 * 1024), 3, MidpointRounding.AwayFromZero);

    private static int CalculateLegacyQuotaUsed(long sizeBytes, int maxSizeMb)
    {
        var maxSizeKilobytes = (long)maxSizeMb * 1024;
        if (maxSizeKilobytes <= 0)
        {
            return 0;
        }

        var currentSizeKilobytes = sizeBytes / 1024;
        return (int)(((float)currentSizeKilobytes / maxSizeKilobytes) * 100);
    }

    private static bool ReadLegacyBoolean(SqlDataReader reader, int ordinal) =>
        Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
}
