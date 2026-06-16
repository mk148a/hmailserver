using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerImapRecentFlagStore : IImapRecentFlagStore
{
    public const string SelectRecentUidsSql = """
SELECT messageuid
FROM hm_messages
WHERE
    messageaccountid = @AccountId
    AND messagefolderid = @FolderId
    AND messagetype = 2
    AND (messageflags & @RecentFlag) = @RecentFlag
ORDER BY messageuid ASC;
""";

    public const string ClearRecentFlagsSql = """
UPDATE hm_messages
SET messageflags = messageflags & ~ @RecentFlag
WHERE
    messageaccountid = @AccountId
    AND messagefolderid = @FolderId
    AND messagetype = 2
    AND (messageflags & @RecentFlag) = @RecentFlag;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerImapRecentFlagStore(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<long>> CaptureRecentUidsAsync(
        int accountId,
        int folderId,
        bool clearRecentFlags,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            var recentUids = await LoadRecentUidsAsync(connection, transaction, accountId, folderId, cancellationToken).ConfigureAwait(false);
            if (clearRecentFlags && recentUids.Count > 0)
            {
                await ClearRecentFlagsAsync(connection, transaction, accountId, folderId, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return recentUids;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async ValueTask<IReadOnlyList<long>> LoadRecentUidsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int accountId,
        int folderId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectRecentUidsSql, connection, transaction);
        AddMailboxParameters(command, accountId, folderId);
        var recentUids = new List<long>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            recentUids.Add(reader.GetInt64(0));
        }

        return recentUids;
    }

    private static async ValueTask ClearRecentFlagsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int accountId,
        int folderId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(ClearRecentFlagsSql, connection, transaction);
        AddMailboxParameters(command, accountId, folderId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddMailboxParameters(
        SqlCommand command,
        int accountId,
        int folderId)
    {
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@FolderId", SqlDbType.Int).Value = folderId;
        command.Parameters.Add("@RecentFlag", SqlDbType.TinyInt).Value = ImapMessageFlags.Recent;
    }
}
