using System.Data;
using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerExternalFetchAccountStore : IExternalFetchAccountStore
{
    public const string LeaseReadyAccountsSql = """
;WITH Candidates AS
(
    SELECT TOP (@BatchSize)
        fa.faid
    FROM hm_fetchaccounts AS fa WITH (UPDLOCK, READPAST, ROWLOCK)
    WHERE
        fa.faactive <> 0
        AND fa.falocked = 0
        AND fa.fanexttry <= SYSUTCDATETIME()
        AND EXISTS
        (
            SELECT 1
            FROM hm_accounts AS a
            INNER JOIN hm_domains AS d ON d.domainid = a.accountdomainid
            WHERE
                a.accountid = fa.faaccountid
                AND a.accountactive <> 0
                AND d.domainactive <> 0
        )
    ORDER BY fa.faid ASC
)
UPDATE fa
SET fa.falocked = 1
OUTPUT
    inserted.faid,
    inserted.faaccountid,
    inserted.faaccountname,
    inserted.faserveraddress,
    inserted.faserverport,
    inserted.faservertype,
    inserted.fausername,
    inserted.fapassword,
    inserted.faminutes,
    inserted.fadaystokeep,
    inserted.faprocessmimerecipients,
    inserted.faprocessmimedate,
    inserted.faconnectionsecurity,
    inserted.fauseantispam,
    inserted.fauseantivirus,
    inserted.faenablerouterecipients,
    inserted.famimerecipientheaders,
    a.accountaddress
FROM hm_fetchaccounts AS fa
INNER JOIN Candidates AS c ON c.faid = fa.faid
INNER JOIN hm_accounts AS a ON a.accountid = fa.faaccountid
INNER JOIN hm_domains AS d ON d.domainid = a.accountdomainid
WHERE
    a.accountactive <> 0
    AND d.domainactive <> 0;
""";

    public const string DeferInactiveAccountsSql = """
UPDATE fa
SET fanexttry = DATEADD(minute, fa.faminutes, SYSUTCDATETIME())
FROM hm_fetchaccounts AS fa
WHERE
    fa.faactive <> 0
    AND fa.falocked = 0
    AND fa.fanexttry <= SYSUTCDATETIME()
    AND NOT EXISTS
    (
        SELECT 1
        FROM hm_accounts AS a
        INNER JOIN hm_domains AS d ON d.domainid = a.accountdomainid
        WHERE
            a.accountid = fa.faaccountid
            AND a.accountactive <> 0
            AND d.domainactive <> 0
    );

SELECT @@ROWCOUNT;
""";

    public const string CompleteSql = """
UPDATE hm_fetchaccounts
SET
    falocked = 0,
    fanexttry = DATEADD(minute, faminutes, SYSUTCDATETIME())
WHERE faid = @FetchAccountId
  AND falocked = 1;

SELECT @@ROWCOUNT;
""";

    public const string ReleaseSql = """
UPDATE hm_fetchaccounts
SET falocked = 0
WHERE faid = @FetchAccountId
  AND falocked = 1;

SELECT @@ROWCOUNT;
""";

    public const string ResetLocksSql = """
UPDATE hm_fetchaccounts
SET falocked = 0
WHERE falocked <> 0;
""";

    public const string SelectKnownUidsSql = """
SELECT uidid, uidvalue, uidtime
FROM hm_fetchaccounts_uids
WHERE uidfaid = @FetchAccountId
ORDER BY uidid ASC;
""";

    public const string InsertKnownUidSql = """
INSERT INTO hm_fetchaccounts_uids
(
    uidfaid,
    uidvalue,
    uidtime
)
VALUES
(
    @FetchAccountId,
    @UidValue,
    SYSUTCDATETIME()
);
""";

    public const string DeleteKnownUidSql = """
DELETE FROM hm_fetchaccounts_uids
WHERE uidid = @UidId;

SELECT @@ROWCOUNT;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerExternalFetchAccountStore(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async IAsyncEnumerable<ExternalFetchAccountLease> LeaseReadyAccountsAsync(
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(LeaseReadyAccountsSql, connection);
        command.Parameters.Add("@BatchSize", SqlDbType.Int).Value = batchSize;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return ReadLease(reader);
        }
    }

    public async ValueTask<int> DeferInactiveAccountsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeferInactiveAccountsSql, connection);
        return await ExecuteInt32ScalarAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> CompleteAsync(
        int fetchAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fetchAccountId);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(CompleteSql, connection);
        AddFetchAccountId(command, fetchAccountId);
        return await ExecuteInt32ScalarAsync(command, cancellationToken).ConfigureAwait(false) == 1;
    }

    public async ValueTask<bool> ReleaseAsync(
        int fetchAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fetchAccountId);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(ReleaseSql, connection);
        AddFetchAccountId(command, fetchAccountId);
        return await ExecuteInt32ScalarAsync(command, cancellationToken).ConfigureAwait(false) == 1;
    }

    public async ValueTask ResetLocksAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(ResetLocksSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<ExternalFetchKnownUid>> LoadKnownUidsAsync(
        int fetchAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fetchAccountId);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(SelectKnownUidsSql, connection);
        AddFetchAccountId(command, fetchAccountId);

        var uids = new List<ExternalFetchKnownUid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            uids.Add(new ExternalFetchKnownUid(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDateTime(2)));
        }

        return uids;
    }

    public async ValueTask AddKnownUidAsync(
        int fetchAccountId,
        string uid,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fetchAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(uid);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertKnownUidSql, connection);
        AddFetchAccountId(command, fetchAccountId);
        command.Parameters.Add("@UidValue", SqlDbType.NVarChar, 255).Value =
            uid.Length <= 255 ? uid : uid[..255];
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> DeleteKnownUidAsync(
        int uidId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(uidId);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteKnownUidSql, connection);
        command.Parameters.Add("@UidId", SqlDbType.Int).Value = uidId;
        return await ExecuteInt32ScalarAsync(command, cancellationToken).ConfigureAwait(false) == 1;
    }

    private static ExternalFetchAccountLease ReadLease(SqlDataReader reader)
    {
        var encryptedPassword = reader.GetString(7);
        var password = LegacyBlowfishPasswordCipher.TryDecrypt(encryptedPassword, out var decryptedPassword)
            ? decryptedPassword
            : encryptedPassword;

        return new ExternalFetchAccountLease(
            FetchAccountId: reader.GetInt32(0),
            AccountId: reader.GetInt32(1),
            Name: reader.GetString(2),
            ServerAddress: reader.GetString(3),
            ServerPort: reader.GetInt32(4),
            ServerType: (ExternalFetchServerType)reader.GetByte(5),
            Username: reader.GetString(6),
            Password: password,
            MinutesBetweenFetch: reader.GetInt32(8),
            DaysToKeep: reader.GetInt32(9),
            ProcessMimeRecipients: ReadTinyIntBoolean(reader, 10),
            ProcessMimeDate: ReadTinyIntBoolean(reader, 11),
            ConnectionSecurity: (ExternalFetchConnectionSecurity)reader.GetByte(12),
            UseAntiSpam: ReadTinyIntBoolean(reader, 13),
            UseAntiVirus: ReadTinyIntBoolean(reader, 14),
            EnableRouteRecipients: ReadTinyIntBoolean(reader, 15),
            MimeRecipientHeaders: reader.GetString(16),
            AccountAddress: reader.GetString(17));
    }

    private static bool ReadTinyIntBoolean(SqlDataReader reader, int ordinal) =>
        reader.GetByte(ordinal) != 0;

    private static void AddFetchAccountId(SqlCommand command, int fetchAccountId) =>
        command.Parameters.Add("@FetchAccountId", SqlDbType.Int).Value = fetchAccountId;

    private static async ValueTask<int> ExecuteInt32ScalarAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is int value ? value : Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
