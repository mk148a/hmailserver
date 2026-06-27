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
            accounts.Add(
                new AccountAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    DomainId: reader.GetInt32(1),
                    Address: reader.GetString(2),
                    Active: Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) != 0,
                    AdminLevel: Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                    MaxSize: reader.GetInt32(5),
                    PersonFirstName: reader.GetString(6),
                    PersonLastName: reader.GetString(7),
                    VacationMessageIsOn: ReadLegacyBoolean(reader, 8),
                    VacationMessage: reader.GetString(9),
                    VacationSubject: reader.GetString(10),
                    VacationMessageExpires: ReadLegacyBoolean(reader, 11),
                    VacationMessageExpiresDate: reader.GetString(12),
                    VacationMessageAbortSpamFlagged: ReadLegacyBoolean(reader, 13),
                    ForwardEnabled: ReadLegacyBoolean(reader, 14),
                    ForwardAddress: reader.GetString(15),
                    ForwardKeepOriginal: ReadLegacyBoolean(reader, 16),
                    ForwardAbortSpamFlagged: ReadLegacyBoolean(reader, 17),
                    SignatureEnabled: ReadLegacyBoolean(reader, 18),
                    SignaturePlainText: reader.GetString(19),
                    SignatureHtml: reader.GetString(20)));
        }

        return accounts;
    }

    private static bool ReadLegacyBoolean(SqlDataReader reader, int ordinal) =>
        Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
}
