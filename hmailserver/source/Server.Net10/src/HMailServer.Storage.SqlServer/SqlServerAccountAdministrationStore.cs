using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerAccountAdministrationStore : IAccountAdministrationStore
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
            var maxSize = reader.GetInt32(8);
            var sizeBytes = Convert.ToInt64(reader.GetValue(9), CultureInfo.InvariantCulture);
            accounts.Add(
                new AccountAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    DomainId: reader.GetInt32(1),
                    Address: reader.GetString(2),
                    Active: Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) != 0,
                    AdminLevel: Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                    IsActiveDirectoryAccount: ReadLegacyBoolean(reader, 5),
                    ActiveDirectoryDomain: reader.GetString(6),
                    ActiveDirectoryUsername: reader.GetString(7),
                    MaxSize: maxSize,
                    Size: CalculateLegacySizeMb(sizeBytes),
                    QuotaUsed: CalculateLegacyQuotaUsed(sizeBytes, maxSize),
                    LastLogonTime: reader.GetDateTime(10),
                    PersonFirstName: reader.GetString(11),
                    PersonLastName: reader.GetString(12),
                    VacationMessageIsOn: ReadLegacyBoolean(reader, 13),
                    VacationMessage: reader.GetString(14),
                    VacationSubject: reader.GetString(15),
                    VacationMessageExpires: ReadLegacyBoolean(reader, 16),
                    VacationMessageExpiresDate: reader.GetString(17),
                    VacationMessageAbortSpamFlagged: ReadLegacyBoolean(reader, 18),
                    ForwardEnabled: ReadLegacyBoolean(reader, 19),
                    ForwardAddress: reader.GetString(20),
                    ForwardKeepOriginal: ReadLegacyBoolean(reader, 21),
                    ForwardAbortSpamFlagged: ReadLegacyBoolean(reader, 22),
                    SignatureEnabled: ReadLegacyBoolean(reader, 23),
                    SignaturePlainText: reader.GetString(24),
                    SignatureHtml: reader.GetString(25)));
        }

        return accounts;
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
