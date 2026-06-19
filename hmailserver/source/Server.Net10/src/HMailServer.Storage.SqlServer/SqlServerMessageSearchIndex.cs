using System.Data;
using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using HMailServer.Search.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerMessageSearchIndex : IMessageSearchIndex
{
    private const string ReadinessSql = """
SELECT CASE
    WHEN CONVERT(int, FULLTEXTSERVICEPROPERTY('IsFullTextInstalled')) = 1
     AND OBJECT_ID(N'dbo.hm_message_search_documents', N'U') IS NOT NULL
     AND EXISTS
     (
        SELECT 1
        FROM sys.fulltext_indexes
        WHERE object_id = OBJECT_ID(N'dbo.hm_message_search_documents')
     )
    THEN 1 ELSE 0 END;
""";

    private const string QueueSql = """
BEGIN TRANSACTION;

UPDATE hm_message_search_queue WITH (UPDLOCK, SERIALIZABLE)
SET
    queuedutc = SYSUTCDATETIME(),
    nextattemptutc = NULL,
    searchleaseowner = NULL,
    searchleaseexpiresutc = NULL,
    lasterror = NULL
WHERE messageid = @MessageId;

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO hm_message_search_queue
    (
        messageid,
        queuedutc,
        attempts,
        lastattemptutc,
        nextattemptutc,
        searchleaseowner,
        searchleaseexpiresutc,
        lasterror
    )
    VALUES
    (
        @MessageId,
        SYSUTCDATETIME(),
        0,
        NULL,
        NULL,
        NULL,
        NULL,
        NULL
    );
END

COMMIT TRANSACTION;
""";

    private const string UpsertSql = """
BEGIN TRANSACTION;

UPDATE hm_message_search_documents WITH (UPDLOCK, SERIALIZABLE)
SET
    messageaccountid = @AccountId,
    messagefolderid = @FolderId,
    messageuid = @Uid,
    messageinternaldateutc = @InternalDateUtc,
    messagesize = @SizeBytes,
    messageflags = @Flags,
    search_header = @HeaderText,
    search_body = @BodyText,
    search_combined = @CombinedText,
    updatedutc = SYSUTCDATETIME()
WHERE messageid = @MessageId;

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO hm_message_search_documents
    (
        messageid,
        messageaccountid,
        messagefolderid,
        messageuid,
        messageinternaldateutc,
        messagesize,
        messageflags,
        search_header,
        search_body,
        search_combined,
        updatedutc
    )
    VALUES
    (
        @MessageId,
        @AccountId,
        @FolderId,
        @Uid,
        @InternalDateUtc,
        @SizeBytes,
        @Flags,
        @HeaderText,
        @BodyText,
        @CombinedText,
        SYSUTCDATETIME()
    );
END

COMMIT TRANSACTION;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly SqlServerImapSearchPlanner _searchPlanner;

    public SqlServerMessageSearchIndex(
        SqlServerConnectionFactory connectionFactory,
        SqlServerImapSearchPlanner searchPlanner)
    {
        _connectionFactory = connectionFactory;
        _searchPlanner = searchPlanner;
    }

    public async ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(ReadinessSql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is int ready && ready == 1;
    }

    public async ValueTask QueueForIndexingAsync(MessageIdentity identity, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(QueueSql, connection);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = identity.MessageId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask UpsertAsync(MessageSearchDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpsertSql, connection);
        AddDocumentParameters(command, document);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<MessageIdentity> SearchAsync(
        ImapSearchRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var plan = _searchPlanner.Plan(request);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(plan.CommandText, connection);

        foreach (var parameter in plan.Parameters)
        {
            AddPlanParameter(command, parameter.Key, parameter.Value);
        }

        await using var reader = await command
            .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return new MessageIdentity(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt64(3));
        }
    }

    private static void AddDocumentParameters(SqlCommand command, MessageSearchDocument document)
    {
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = document.Identity.MessageId;
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = document.Identity.AccountId;
        command.Parameters.Add("@FolderId", SqlDbType.Int).Value = document.Identity.FolderId;
        command.Parameters.Add("@Uid", SqlDbType.BigInt).Value = document.Identity.Uid;
        command.Parameters.Add("@InternalDateUtc", SqlDbType.DateTime2).Value = document.InternalDateUtc.UtcDateTime;
        command.Parameters.Add("@SizeBytes", SqlDbType.BigInt).Value = document.SizeBytes;
        command.Parameters.Add("@Flags", SqlDbType.TinyInt).Value = document.Flags;
        command.Parameters.Add("@HeaderText", SqlDbType.NVarChar, -1).Value = document.HeaderText;
        command.Parameters.Add("@BodyText", SqlDbType.NVarChar, -1).Value = document.BodyText;
        command.Parameters.Add("@CombinedText", SqlDbType.NVarChar, -1).Value = document.CombinedText;
    }

    private static void AddPlanParameter(SqlCommand command, string name, object value)
    {
        var parameter = value switch
        {
            int typed => new SqlParameter(name, SqlDbType.Int) { Value = typed },
            long typed => new SqlParameter(name, SqlDbType.BigInt) { Value = typed },
            byte typed => new SqlParameter(name, SqlDbType.TinyInt) { Value = typed },
            DateTime typed => new SqlParameter(name, SqlDbType.DateTime2) { Value = typed },
            string typed => new SqlParameter(name, SqlDbType.NVarChar, 4000) { Value = typed },
            _ => throw new NotSupportedException($"Unsupported SQL search parameter type {value.GetType().FullName}.")
        };

        command.Parameters.Add(parameter);
    }
}
