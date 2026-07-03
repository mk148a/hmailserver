using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerMessageAdministrationStore : IMessageAdministrationStore
{
    public const string GetAccountMessagesSql = """
SELECT
    messageid,
    messageaccountid,
    messagefolderid,
    messagefilename,
    messagetype,
    messagefrom,
    messagesize,
    messagecurnooftries,
    messageflags,
    messagecreatetime,
    messageuid
FROM hm_messages
WHERE messageaccountid = @AccountID
  AND messagetype = 2
ORDER BY messageid ASC;
""";

    public const string GetFolderMessagesSql = """
SELECT
    messageid,
    messageaccountid,
    messagefolderid,
    messagefilename,
    messagetype,
    messagefrom,
    messagesize,
    messagecurnooftries,
    messageflags,
    messagecreatetime,
    messageuid
FROM hm_messages
WHERE messagefolderid = @FolderID
  AND messagetype = 2
ORDER BY messageuid ASC, messageid ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerMessageAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetAccountMessagesAsync(
        int accountId,
        CancellationToken cancellationToken) =>
        GetMessagesAsync(GetAccountMessagesSql, "@AccountID", accountId, cancellationToken);

    public ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetFolderMessagesAsync(
        int folderId,
        CancellationToken cancellationToken) =>
        GetMessagesAsync(GetFolderMessagesSql, "@FolderID", folderId, cancellationToken);

    private async ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetMessagesAsync(
        string sql,
        string parameterName,
        int parameterValue,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(parameterName, SqlDbType.Int).Value = parameterValue;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var messages = new List<MessageAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            messages.Add(
                new MessageAdministrationSnapshot(
                    Id: Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    AccountId: Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                    FolderId: Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                    FileName: reader.GetString(3),
                    State: Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                    FromAddress: reader.GetString(5),
                    SizeBytes: Convert.ToInt64(reader.GetValue(6), CultureInfo.InvariantCulture),
                    CurrentNumberOfTries: Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture),
                    Flags: Convert.ToInt32(reader.GetValue(8), CultureInfo.InvariantCulture),
                    InternalDate: reader.GetDateTime(9),
                    Uid: Convert.ToInt64(reader.GetValue(10), CultureInfo.InvariantCulture)));
        }

        return messages;
    }
}
