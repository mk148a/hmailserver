using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerMessageIndexingAdministrationStore : IMessageIndexingAdministrationStore
{
    public const string StatusSql = """
SELECT
    (SELECT COUNT(*) FROM hm_messages WHERE messagetype = 2),
    (SELECT COUNT(*) FROM hm_message_search_documents),
    CASE WHEN EXISTS
    (
        SELECT 1
        FROM hm_settings
        WHERE settingname = N'MessageIndexing'
          AND settinginteger <> 0
    )
    THEN 1 ELSE 0 END,
    CASE
        WHEN CONVERT(int, FULLTEXTSERVICEPROPERTY('IsFullTextInstalled')) = 1
         AND OBJECT_ID(N'dbo.hm_message_search_documents', N'U') IS NOT NULL
         AND EXISTS
         (
            SELECT 1
            FROM sys.fulltext_indexes
            WHERE object_id = OBJECT_ID(N'dbo.hm_message_search_documents')
         )
        THEN 1 ELSE 0
    END,
    (SELECT COUNT(*) FROM hm_message_search_queue),
    COALESCE
    (
        (
            SELECT TOP (1) lasterror
            FROM hm_message_search_queue
            WHERE lasterror IS NOT NULL
            ORDER BY COALESCE(lastattemptutc, queuedutc) DESC, messageid DESC
        ),
        N''
    );
""";

    public const string IsEnabledSql = """
SELECT CASE WHEN EXISTS
(
    SELECT 1
    FROM hm_settings
    WHERE settingname = N'MessageIndexing'
      AND settinginteger <> 0
)
THEN 1 ELSE 0 END;
""";

    public const string SetEnabledSql = """
BEGIN TRANSACTION;

UPDATE hm_settings WITH (UPDLOCK, SERIALIZABLE)
SET settinginteger = @Enabled
WHERE settingname = N'MessageIndexing';

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO hm_settings (settingname, settingstring, settinginteger)
    VALUES (N'MessageIndexing', N'', @Enabled);
END

IF @Enabled <> 0
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
    SELECT
        m.messageid,
        SYSUTCDATETIME(),
        0,
        NULL,
        NULL,
        NULL,
        NULL,
        NULL
    FROM hm_messages AS m
    WHERE m.messagetype = 2
      AND NOT EXISTS
      (
          SELECT 1
          FROM hm_message_search_documents AS d
          WHERE d.messageid = m.messageid
      )
      AND NOT EXISTS
      (
          SELECT 1
          FROM hm_message_search_queue AS q WITH (UPDLOCK, SERIALIZABLE)
          WHERE q.messageid = m.messageid
      );
END

COMMIT TRANSACTION;
""";

    public const string ClearSql = """
BEGIN TRANSACTION;

DELETE FROM hm_message_search_queue;
DELETE FROM hm_message_search_documents;

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
SELECT
    m.messageid,
    SYSUTCDATETIME(),
    0,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL
FROM hm_messages AS m
WHERE m.messagetype = 2;

COMMIT TRANSACTION;
""";

    public const string IndexSql = """
BEGIN TRANSACTION;

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
SELECT
    m.messageid,
    SYSUTCDATETIME(),
    0,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL
FROM hm_messages AS m
WHERE m.messagetype = 2
  AND NOT EXISTS
  (
      SELECT 1
      FROM hm_message_search_documents AS d
      WHERE d.messageid = m.messageid
  )
  AND NOT EXISTS
  (
      SELECT 1
      FROM hm_message_search_queue AS q WITH (UPDLOCK, SERIALIZABLE)
      WHERE q.messageid = m.messageid
  );

COMMIT TRANSACTION;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerMessageIndexingAdministrationStore(
        SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<MessageIndexingAdministrationStatus> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(StatusSql, connection);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SingleRow,
            cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Message-indexing status query returned no row.");
        }

        return new MessageIndexingAdministrationStatus(
            TotalMessageCount: reader.GetInt32(0),
            TotalIndexedCount: reader.GetInt32(1),
            Enabled: reader.GetInt32(2) != 0,
            IsFullTextReady: reader.GetInt32(3) != 0,
            QueuedMessageCount: reader.GetInt32(4),
            LastError: reader.GetString(5));
    }

    public async ValueTask<bool> IsEnabledAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(IsEnabledSql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture) != 0;
    }

    public async ValueTask SetEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(SetEnabledSql, connection);
        command.Parameters.Add("@Enabled", SqlDbType.Int).Value = enabled ? 1 : 0;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(ClearSql, cancellationToken);

    public ValueTask IndexAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(IndexSql, cancellationToken);

    public ValueTask RebuildAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(ClearSql, cancellationToken);

    private async ValueTask ExecuteAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
