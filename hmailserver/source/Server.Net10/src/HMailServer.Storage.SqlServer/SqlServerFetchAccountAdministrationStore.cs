using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerFetchAccountAdministrationStore : IFetchAccountAdministrationStore
{
    public const string GetFetchAccountsSql = """
SELECT
    faid,
    faaccountid,
    faaccountname,
    faserveraddress,
    faserverport,
    faservertype,
    fausername,
    faminutes,
    fadaystokeep,
    faactive,
    faprocessmimerecipients,
    faprocessmimedate,
    faconnectionsecurity,
    fauseantispam,
    fauseantivirus,
    faenablerouterecipients,
    famimerecipientheaders,
    CONVERT(varchar(19), fanexttry, 120) AS fanexttry,
    falocked
FROM hm_fetchaccounts
WHERE faaccountid = @AccountID
ORDER BY faid ASC;
""";

    public const string SetRetryNowSql = """
UPDATE hm_fetchaccounts
SET fanexttry = GETDATE()
WHERE faid = @FetchAccountID
  AND faaccountid = @AccountID;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerFetchAccountAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<FetchAccountAdministrationSnapshot>> GetFetchAccountsAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetFetchAccountsSql, connection);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var accounts = new List<FetchAccountAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            accounts.Add(
                new FetchAccountAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    AccountId: reader.GetInt32(1),
                    Name: reader.GetString(2),
                    ServerAddress: reader.GetString(3),
                    Port: reader.GetInt32(4),
                    ServerType: Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture),
                    Username: reader.GetString(6),
                    MinutesBetweenFetch: reader.GetInt32(7),
                    DaysToKeepMessages: reader.GetInt32(8),
                    Enabled: ReadLegacyBoolean(reader, 9),
                    ProcessMimeRecipients: ReadLegacyBoolean(reader, 10),
                    ProcessMimeDate: ReadLegacyBoolean(reader, 11),
                    ConnectionSecurity: Convert.ToInt32(reader.GetValue(12), CultureInfo.InvariantCulture),
                    UseAntiSpam: ReadLegacyBoolean(reader, 13),
                    UseAntiVirus: ReadLegacyBoolean(reader, 14),
                    EnableRouteRecipients: ReadLegacyBoolean(reader, 15),
                    MimeRecipientHeaders: reader.GetString(16),
                    NextDownloadTime: reader.GetString(17),
                    IsLocked: ReadLegacyBoolean(reader, 18)));
        }

        return accounts;
    }

    public async ValueTask SetRetryNowAsync(
        int accountId,
        int fetchAccountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(SetRetryNowSql, connection);
        command.Parameters.Add("@FetchAccountID", SqlDbType.Int).Value = fetchAccountId;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool ReadLegacyBoolean(SqlDataReader reader, int ordinal) =>
        Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
}
