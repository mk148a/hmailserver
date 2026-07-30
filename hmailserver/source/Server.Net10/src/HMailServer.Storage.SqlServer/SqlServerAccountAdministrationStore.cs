using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
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

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerAccountAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> GetAccountsAsync(
        int domainId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetAccountsSql, connection);
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = domainId;

        return await ReadAccountsAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<AccountAdministrationSnapshot?> GetAccountByIdAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetAccountByIdSql, connection);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;

        return (await ReadAccountsAsync(command, cancellationToken).ConfigureAwait(false)).FirstOrDefault();
    }

    public async ValueTask<IReadOnlyList<AccountBackupAdministrationSnapshot>> GetBackupAccountsAsync(
        int domainId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetBackupAccountsSql, connection);
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = domainId;

        return await ReadBackupAccountsAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> ReadAccountsAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
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
            CommandBehavior.SequentialAccess,
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
