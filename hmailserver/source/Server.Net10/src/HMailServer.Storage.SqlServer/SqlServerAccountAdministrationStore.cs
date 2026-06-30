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
    accountmaxsize,
    (
        SELECT COALESCE(SUM(CAST(messagesize AS bigint)), 0)
        FROM hm_messages
        WHERE messageaccountid = hm_accounts.accountid
    ) AS accountsizebytes,
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
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var accounts = new List<AccountAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var maxSize = reader.GetInt32(5);
            var sizeBytes = Convert.ToInt64(reader.GetValue(6), CultureInfo.InvariantCulture);
            accounts.Add(
                new AccountAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    DomainId: reader.GetInt32(1),
                    Address: reader.GetString(2),
                    Active: Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) != 0,
                    AdminLevel: Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                    MaxSize: maxSize,
                    Size: CalculateLegacySizeMb(sizeBytes),
                    QuotaUsed: CalculateLegacyQuotaUsed(sizeBytes, maxSize),
                    PersonFirstName: reader.GetString(7),
                    PersonLastName: reader.GetString(8),
                    VacationMessageIsOn: ReadLegacyBoolean(reader, 9),
                    VacationMessage: reader.GetString(10),
                    VacationSubject: reader.GetString(11),
                    VacationMessageExpires: ReadLegacyBoolean(reader, 12),
                    VacationMessageExpiresDate: reader.GetString(13),
                    VacationMessageAbortSpamFlagged: ReadLegacyBoolean(reader, 14),
                    ForwardEnabled: ReadLegacyBoolean(reader, 15),
                    ForwardAddress: reader.GetString(16),
                    ForwardKeepOriginal: ReadLegacyBoolean(reader, 17),
                    ForwardAbortSpamFlagged: ReadLegacyBoolean(reader, 18),
                    SignatureEnabled: ReadLegacyBoolean(reader, 19),
                    SignaturePlainText: reader.GetString(20),
                    SignatureHtml: reader.GetString(21)));
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
