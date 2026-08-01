using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerBackupFetchAccountAdministrationStore : IBackupFetchAccountAdministrationStore
{
    public const string GetBackupFetchAccountsSql = """
SELECT
    faid,
    faaccountid,
    faaccountname,
    faserveraddress,
    faserverport,
    faservertype,
    fausername,
    fapassword,
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

    public const string GetBackupFetchAccountUidsSql = """
SELECT
    uidvalue,
    CONVERT(varchar(19), uidtime, 120) AS uidtime
FROM hm_fetchaccounts_uids
WHERE uidfaid = @FetchAccountID;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerBackupFetchAccountAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<FetchAccountBackupAdministrationSnapshot>> GetBackupFetchAccountsAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetBackupFetchAccountsSql, connection);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;

        var rows = new List<(FetchAccountAdministrationSnapshot Account, string Password)>();
        await using (var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((ReadFetchAccountSnapshot(reader), reader.GetString(7)));
            }
        }

        var accounts = new List<FetchAccountBackupAdministrationSnapshot>(rows.Count);
        foreach (var row in rows)
        {
            accounts.Add(
                new FetchAccountBackupAdministrationSnapshot(
                    row.Account,
                    row.Password,
                    await ReadUidsAsync(connection, row.Account.Id, cancellationToken).ConfigureAwait(false)));
        }

        return accounts;
    }

    private static async ValueTask<IReadOnlyList<FetchAccountUidBackupAdministrationSnapshot>> ReadUidsAsync(
        SqlConnection connection,
        int fetchAccountId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(GetBackupFetchAccountUidsSql, connection);
        command.Parameters.Add("@FetchAccountID", SqlDbType.Int).Value = fetchAccountId;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var uids = new List<FetchAccountUidBackupAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            uids.Add(new FetchAccountUidBackupAdministrationSnapshot(
                reader.GetString(0),
                reader.GetString(1)));
        }

        return uids;
    }

    private static FetchAccountAdministrationSnapshot ReadFetchAccountSnapshot(SqlDataReader reader) =>
        new(
            Id: reader.GetInt32(0),
            AccountId: reader.GetInt32(1),
            Name: reader.GetString(2),
            ServerAddress: reader.GetString(3),
            Port: reader.GetInt32(4),
            ServerType: Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture),
            Username: reader.GetString(6),
            MinutesBetweenFetch: reader.GetInt32(8),
            DaysToKeepMessages: reader.GetInt32(9),
            Enabled: ReadLegacyBoolean(reader, 10),
            ProcessMimeRecipients: ReadLegacyBoolean(reader, 11),
            ProcessMimeDate: ReadLegacyBoolean(reader, 12),
            ConnectionSecurity: Convert.ToInt32(reader.GetValue(13), CultureInfo.InvariantCulture),
            UseAntiSpam: ReadLegacyBoolean(reader, 14),
            UseAntiVirus: ReadLegacyBoolean(reader, 15),
            EnableRouteRecipients: ReadLegacyBoolean(reader, 16),
            MimeRecipientHeaders: reader.GetString(17),
            NextDownloadTime: reader.GetString(18),
            IsLocked: ReadLegacyBoolean(reader, 19));

    private static bool ReadLegacyBoolean(SqlDataReader reader, int ordinal) =>
        Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
}
