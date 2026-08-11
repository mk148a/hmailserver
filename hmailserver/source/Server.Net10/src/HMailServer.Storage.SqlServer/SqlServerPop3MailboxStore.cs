using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerPop3MailboxStore : IPop3MailboxStore
{
    public const string SelectInboxMessagesSql = """
SELECT
    m.messageid,
    m.messageuid,
    m.messagesize
FROM hm_messages AS m WITH (READCOMMITTEDLOCK)
INNER JOIN hm_imapfolders AS f WITH (READCOMMITTEDLOCK)
    ON f.folderid = m.messagefolderid
    AND f.folderaccountid = m.messageaccountid
    AND f.folderparentid = -1
    AND f.foldername = N'Inbox'
WHERE
    m.messageaccountid = @AccountId
    AND m.messagetype = 2
ORDER BY m.messageuid ASC;
""";

    public const string SelectMessageFileSql = """
SELECT TOP (1)
    m.messageid,
    m.messageaccountid,
    m.messagefolderid,
    m.messagefilename,
    a.accountaddress
FROM hm_messages AS m WITH (READCOMMITTEDLOCK)
INNER JOIN hm_imapfolders AS f WITH (READCOMMITTEDLOCK)
    ON f.folderid = m.messagefolderid
    AND f.folderaccountid = m.messageaccountid
    AND f.folderparentid = -1
    AND f.foldername = N'Inbox'
LEFT JOIN hm_accounts AS a
    ON a.accountid = m.messageaccountid
WHERE
    m.messageid = @MessageId
    AND m.messageaccountid = @AccountId
    AND m.messagetype = 2;
""";

    private const string SelectMessagesForDeleteSqlPrefix = """
SELECT
    m.messageid,
    m.messageaccountid,
    m.messagefolderid,
    m.messagefilename,
    a.accountaddress
FROM hm_messages AS m WITH (READCOMMITTEDLOCK)
INNER JOIN hm_imapfolders AS f WITH (READCOMMITTEDLOCK)
    ON f.folderid = m.messagefolderid
    AND f.folderaccountid = m.messageaccountid
    AND f.folderparentid = -1
    AND f.foldername = N'Inbox'
LEFT JOIN hm_accounts AS a
    ON a.accountid = m.messageaccountid
WHERE
    m.messageaccountid = @AccountId
    AND m.messagetype = 2
    AND m.messageid IN (
""";

    private const string DeleteMessagesSqlPrefix = """
DELETE FROM hm_message_search_queue
WHERE messageid IN (
""";

    private const string DeleteMessagesSqlMiddle = """
);

DELETE FROM hm_message_search_documents
WHERE messageid IN (
""";

    private const string DeleteMetadataSqlMiddle = """
);

DELETE FROM hm_message_metadata
WHERE metadata_messageid IN (
""";

    private const string DeleteMessageRowsSqlMiddle = """
);

DELETE FROM hm_messages
WHERE
    messageaccountid = @AccountId
    AND messagetype = 2
    AND messageid IN (
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly MessageFilePathResolver _pathResolver;

    public SqlServerPop3MailboxStore(
        SqlServerConnectionFactory connectionFactory,
        MessageFilePathResolver pathResolver)
    {
        _connectionFactory = connectionFactory;
        _pathResolver = pathResolver;
    }

    public async ValueTask<IReadOnlyList<Pop3MessageListing>> ListMessagesAsync(
        ImapAuthenticatedAccount account,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(SelectInboxMessagesSql, connection);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = account.AccountId;

        var messages = new List<Pop3MessageListing>();
        await using var reader = await command
            .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var messageId = reader.GetInt64(0);
            var messageUid = reader.GetInt64(1);
            messages.Add(
                new Pop3MessageListing(
                    messageId,
                    messageUid.ToString(CultureInfo.InvariantCulture),
                    reader.GetInt64(2)));
        }

        return messages;
    }

    public async ValueTask<Stream> OpenMessageAsync(
        ImapAuthenticatedAccount account,
        long messageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(messageId);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var row = await LoadSingleMessageFileRowAsync(connection, account.AccountId, messageId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            throw new IOException("POP3 message row is missing.");
        }

        var path = ResolveMessagePath(row);
        if (path is null || !File.Exists(path))
        {
            throw new IOException("POP3 message content file is missing.");
        }

        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    public async ValueTask DeleteMessagesAsync(
        ImapAuthenticatedAccount account,
        IReadOnlyCollection<long> messageIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(messageIds);

        if (messageIds.Count == 0)
        {
            return;
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var deleteRows = await LoadMessagesForDeleteAsync(connection, account.AccountId, messageIds, cancellationToken)
            .ConfigureAwait(false);
        if (deleteRows.Count == 0)
        {
            return;
        }

        var deletePlan = PlanDeleteMessages(account.AccountId, deleteRows.Select(row => row.MessageId).ToArray());
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = new SqlCommand(deletePlan.CommandText, connection, transaction);
            AddPlanParameters(command, deletePlan.Parameters);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        foreach (var row in deleteRows)
        {
            TryDeleteMessageFile(row);
        }
    }

    public static SqlMessageFetchPlan PlanSelectMessagesForDelete(
        int accountId,
        IReadOnlyCollection<long> messageIds)
    {
        var parameters = BuildMessageIdParameters(accountId, messageIds, out var inClause);
        return new SqlMessageFetchPlan(
            SelectMessagesForDeleteSqlPrefix + inClause + "\r\n)\r\nORDER BY m.messageuid ASC;",
            parameters);
    }

    public static SqlMessageFetchPlan PlanDeleteMessages(
        int accountId,
        IReadOnlyCollection<long> messageIds)
    {
        var parameters = BuildMessageIdParameters(accountId, messageIds, out var inClause);
        var commandText =
            DeleteMessagesSqlPrefix + inClause + "\r\n" +
            DeleteMessagesSqlMiddle + inClause + "\r\n" +
            DeleteMetadataSqlMiddle + inClause + "\r\n" +
            DeleteMessageRowsSqlMiddle + inClause + "\r\n);";
        return new SqlMessageFetchPlan(commandText, parameters);
    }

    private async ValueTask<MessageFileRow?> LoadSingleMessageFileRowAsync(
        SqlConnection connection,
        int accountId,
        long messageId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectMessageFileSql, connection);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadMessageFileRow(reader)
            : null;
    }

    private async ValueTask<IReadOnlyList<MessageFileRow>> LoadMessagesForDeleteAsync(
        SqlConnection connection,
        int accountId,
        IReadOnlyCollection<long> messageIds,
        CancellationToken cancellationToken)
    {
        var plan = PlanSelectMessagesForDelete(accountId, messageIds);
        await using var command = new SqlCommand(plan.CommandText, connection);
        AddPlanParameters(command, plan.Parameters);

        var rows = new List<MessageFileRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(ReadMessageFileRow(reader));
        }

        return rows;
    }

    private string? ResolveMessagePath(MessageFileRow row) =>
        _pathResolver.Resolve(
            row.MessageFileName,
            row.AccountId,
            row.FolderId,
            row.AccountAddress);

    private void TryDeleteMessageFile(MessageFileRow row)
    {
        var path = ResolveMessagePath(row);
        if (path is null || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static MessageFileRow ReadMessageFileRow(SqlDataReader reader) =>
        new(
            reader.GetInt64(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4));

    private static Dictionary<string, object> BuildMessageIdParameters(
        int accountId,
        IReadOnlyCollection<long> messageIds,
        out string inClause)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountId);
        ArgumentNullException.ThrowIfNull(messageIds);
        if (messageIds.Count == 0)
        {
            throw new ArgumentException("At least one message id is required.", nameof(messageIds));
        }

        var parameters = new Dictionary<string, object>
        {
            ["@AccountId"] = accountId
        };
        var parameterNames = new List<string>(messageIds.Count);
        foreach (var messageId in messageIds.Distinct().Order())
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(messageId);
            var parameterName = $"@MessageId{parameterNames.Count}";
            parameters[parameterName] = messageId;
            parameterNames.Add(parameterName);
        }

        inClause = string.Join(", ", parameterNames);
        return parameters;
    }

    private static void AddPlanParameters(
        SqlCommand command,
        IReadOnlyDictionary<string, object> parameters)
    {
        foreach (var parameter in parameters)
        {
            var sqlParameter = parameter.Value switch
            {
                int typed => new SqlParameter(parameter.Key, SqlDbType.Int) { Value = typed },
                long typed => new SqlParameter(parameter.Key, SqlDbType.BigInt) { Value = typed },
                _ => throw new NotSupportedException(
                    $"Unsupported SQL POP3 parameter type {parameter.Value.GetType().FullName}.")
            };

            command.Parameters.Add(sqlParameter);
        }
    }

    private sealed record MessageFileRow(
        long MessageId,
        int AccountId,
        int FolderId,
        string MessageFileName,
        string? AccountAddress);
}
