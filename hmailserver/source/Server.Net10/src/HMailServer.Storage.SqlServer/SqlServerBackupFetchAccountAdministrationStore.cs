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

    private readonly SqlServerConnectionFactory? _connectionFactory;
    private readonly SqlServerBackupRestoreTransactionContext? _transactionContext;

    public SqlServerBackupFetchAccountAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    internal SqlServerBackupFetchAccountAdministrationStore(
        SqlServerBackupRestoreTransactionContext transactionContext)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        _transactionContext = transactionContext;
    }

    public async ValueTask<IReadOnlyList<FetchAccountBackupAdministrationSnapshot>> GetBackupFetchAccountsAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var lease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            GetBackupFetchAccountsSql,
            cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;

        var rows = new List<(FetchAccountAdministrationSnapshot Account, string Password)>();
        await using (var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(ReadFetchAccountSnapshot(reader));
            }
        }

        var accounts = new List<FetchAccountBackupAdministrationSnapshot>(rows.Count);
        foreach (var row in rows)
        {
            accounts.Add(
                new FetchAccountBackupAdministrationSnapshot(
                    row.Account,
                    row.Password,
                    await ReadUidsAsync(
                        _connectionFactory,
                        _transactionContext,
                        row.Account.Id,
                        cancellationToken).ConfigureAwait(false)));
        }

        return accounts;
    }

    private static async ValueTask<IReadOnlyList<FetchAccountUidBackupAdministrationSnapshot>> ReadUidsAsync(
        SqlServerConnectionFactory? connectionFactory,
        SqlServerBackupRestoreTransactionContext? transactionContext,
        int fetchAccountId,
        CancellationToken cancellationToken)
    {
        await using var lease = await SqlServerCommandLease.OpenAsync(
            connectionFactory,
            transactionContext,
            GetBackupFetchAccountUidsSql,
            cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
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

    private static (FetchAccountAdministrationSnapshot Account, string Password) ReadFetchAccountSnapshot(
        SqlDataReader reader)
    {
        var id = reader.GetInt32(0);
        var accountId = reader.GetInt32(1);
        var name = reader.GetString(2);
        var serverAddress = reader.GetString(3);
        var port = reader.GetInt32(4);
        var serverType = Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture);
        var username = reader.GetString(6);
        var password = reader.GetString(7);
        var minutesBetweenFetch = reader.GetInt32(8);
        var daysToKeepMessages = reader.GetInt32(9);
        var enabled = ReadLegacyBoolean(reader, 10);
        var processMimeRecipients = ReadLegacyBoolean(reader, 11);
        var processMimeDate = ReadLegacyBoolean(reader, 12);
        var connectionSecurity = Convert.ToInt32(reader.GetValue(13), CultureInfo.InvariantCulture);
        var useAntiSpam = ReadLegacyBoolean(reader, 14);
        var useAntiVirus = ReadLegacyBoolean(reader, 15);
        var enableRouteRecipients = ReadLegacyBoolean(reader, 16);
        var mimeRecipientHeaders = reader.GetString(17);
        var nextDownloadTime = reader.GetString(18);
        var isLocked = ReadLegacyBoolean(reader, 19);

        return (
            new FetchAccountAdministrationSnapshot(
                Id: id,
                AccountId: accountId,
                Name: name,
                ServerAddress: serverAddress,
                Port: port,
                ServerType: serverType,
                Username: username,
                MinutesBetweenFetch: minutesBetweenFetch,
                DaysToKeepMessages: daysToKeepMessages,
                Enabled: enabled,
                ProcessMimeRecipients: processMimeRecipients,
                ProcessMimeDate: processMimeDate,
                ConnectionSecurity: connectionSecurity,
                UseAntiSpam: useAntiSpam,
                UseAntiVirus: useAntiVirus,
                EnableRouteRecipients: enableRouteRecipients,
                MimeRecipientHeaders: mimeRecipientHeaders,
                NextDownloadTime: nextDownloadTime,
                IsLocked: isLocked),
            password);
    }

    private static bool ReadLegacyBoolean(SqlDataReader reader, int ordinal) =>
        Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
}
