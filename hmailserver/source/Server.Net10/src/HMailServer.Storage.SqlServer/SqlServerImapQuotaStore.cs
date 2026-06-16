using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerImapQuotaStore : IImapQuotaStore
{
    private const string AccountQuotaRoot = "";

    public const string SelectQuotaEnabledSql = """
SELECT TOP (1) settinginteger
FROM hm_settings
WHERE settingname = N'enableimapquota';
""";

    public const string SelectQuotaSnapshotSql = """
SELECT
    a.accountmaxsize,
    a.accountadminlevel,
    d.domainlimitationsenabled,
    d.domainmaxaccountsize,
    CONVERT(bigint, COALESCE(SUM(CASE WHEN m.messagetype = 2 THEN m.messagesize ELSE 0 END), 0)) AS usedbytes
FROM hm_accounts AS a
INNER JOIN hm_domains AS d
    ON d.domainid = a.accountdomainid
LEFT JOIN hm_messages AS m
    ON m.messageaccountid = a.accountid
WHERE
    a.accountid = @AccountId
    AND a.accountactive <> 0
    AND d.domainactive <> 0
GROUP BY
    a.accountmaxsize,
    a.accountadminlevel,
    d.domainlimitationsenabled,
    d.domainmaxaccountsize;
""";

    public const string UpdateAccountQuotaSql = """
UPDATE hm_accounts
SET accountmaxsize = @MaxSizeMb
WHERE accountid = @AccountId;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerImapQuotaStore(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<ImapQuotaResult> GetQuotaAsync(
        int requesterAccountId,
        string quotaRoot,
        CancellationToken cancellationToken)
    {
        if (!quotaRoot.Equals(AccountQuotaRoot, StringComparison.Ordinal))
        {
            return ImapQuotaResult.Failure(ImapQuotaCommandStatus.QuotaRootNotFound);
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await IsQuotaEnabledAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return ImapQuotaResult.Failure(ImapQuotaCommandStatus.QuotaDisabled);
        }

        var snapshot = await LoadQuotaSnapshotAsync(connection, requesterAccountId, cancellationToken).ConfigureAwait(false);
        return snapshot is null
            ? ImapQuotaResult.Failure(ImapQuotaCommandStatus.AccountNotFound)
            : new ImapQuotaResult(ImapQuotaCommandStatus.Success, snapshot.ToQuota(AccountQuotaRoot));
    }

    public async ValueTask<ImapQuotaRootResult> GetQuotaRootAsync(
        int requesterAccountId,
        string mailboxName,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await IsQuotaEnabledAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return ImapQuotaRootResult.Failure(ImapQuotaCommandStatus.QuotaDisabled);
        }

        var snapshot = await LoadQuotaSnapshotAsync(connection, requesterAccountId, cancellationToken).ConfigureAwait(false);
        return snapshot is null
            ? ImapQuotaRootResult.Failure(ImapQuotaCommandStatus.AccountNotFound)
            : new ImapQuotaRootResult(
                ImapQuotaCommandStatus.Success,
                mailboxName,
                snapshot.ToQuota(AccountQuotaRoot));
    }

    public async ValueTask<ImapQuotaMutationResult> SetQuotaAsync(
        int requesterAccountId,
        string quotaRoot,
        long limitKilobytes,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limitKilobytes);

        if (!quotaRoot.Equals(AccountQuotaRoot, StringComparison.Ordinal))
        {
            return new ImapQuotaMutationResult(ImapQuotaCommandStatus.QuotaRootNotFound);
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await IsQuotaEnabledAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return new ImapQuotaMutationResult(ImapQuotaCommandStatus.QuotaDisabled);
        }

        var snapshot = await LoadQuotaSnapshotAsync(connection, requesterAccountId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return new ImapQuotaMutationResult(ImapQuotaCommandStatus.AccountNotFound);
        }

        if (!snapshot.IsAdministrator)
        {
            return new ImapQuotaMutationResult(ImapQuotaCommandStatus.PermissionDenied);
        }

        var maxSizeMb = ConvertKilobytesToMegabytes(limitKilobytes);
        await using var command = new SqlCommand(UpdateAccountQuotaSql, connection);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = requesterAccountId;
        command.Parameters.Add("@MaxSizeMb", SqlDbType.Int).Value = maxSizeMb;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return new ImapQuotaMutationResult(ImapQuotaCommandStatus.Success);
    }

    private static int ConvertKilobytesToMegabytes(long limitKilobytes)
    {
        if (limitKilobytes == 0)
        {
            return 0;
        }

        var megabytes = (limitKilobytes + 1023) / 1024;
        if (megabytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(limitKilobytes), "Quota limit is too large for hMailServer accountmaxsize.");
        }

        return (int)megabytes;
    }

    private static async ValueTask<bool> IsQuotaEnabledAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectQuotaEnabledSql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is null || Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture) != 0;
    }

    private static async ValueTask<QuotaSnapshot?> LoadQuotaSnapshotAsync(
        SqlConnection connection,
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectQuotaSnapshotSql, connection);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new QuotaSnapshot(
            AccountMaxSizeMb: reader.GetInt32(0),
            AdminLevel: reader.GetByte(1),
            DomainLimitationsEnabled: reader.GetInt32(2) != 0,
            DomainMaxAccountSizeMb: reader.GetInt32(3),
            UsedBytes: reader.GetInt64(4));
    }

    private sealed record QuotaSnapshot(
        int AccountMaxSizeMb,
        byte AdminLevel,
        bool DomainLimitationsEnabled,
        int DomainMaxAccountSizeMb,
        long UsedBytes)
    {
        public bool IsAdministrator => AdminLevel > 0;

        public ImapQuota ToQuota(string rootName)
        {
            var limitMb = AccountMaxSizeMb > 0
                ? AccountMaxSizeMb
                : DomainLimitationsEnabled && DomainMaxAccountSizeMb > 0
                    ? DomainMaxAccountSizeMb
                    : (int?)null;
            return new ImapQuota(
                rootName,
                UsedKilobytes: UsedBytes / 1024,
                LimitKilobytes: limitMb is null ? null : (long)limitMb.Value * 1024);
        }
    }
}
