using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerImapSequenceNumberResolver : IImapSequenceNumberResolver
{
    public const string SequenceSnapshotSql = """
SELECT
    m.messageid,
    CONVERT(bigint, ROW_NUMBER() OVER (ORDER BY m.messageuid ASC)) AS sequencenumber
FROM hm_messages AS m WITH (READCOMMITTEDLOCK)
WHERE
    m.messagetype = 2
    AND m.messageaccountid = @AccountId
    AND m.messagefolderid = @FolderId
ORDER BY m.messageuid ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerImapSequenceNumberResolver(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyDictionary<long, long>> ResolveMailboxSequenceNumbersAsync(
        int accountId,
        int folderId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(folderId);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(SequenceSnapshotSql, connection);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@FolderId", SqlDbType.Int).Value = folderId;

        var sequenceNumbers = new Dictionary<long, long>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            sequenceNumbers[reader.GetInt64(0)] = reader.GetInt64(1);
        }

        return sequenceNumbers;
    }
}
