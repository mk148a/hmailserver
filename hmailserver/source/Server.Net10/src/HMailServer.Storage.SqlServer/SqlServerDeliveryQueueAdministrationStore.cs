using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDeliveryQueueAdministrationStore : IDeliveryQueueAdministrationStore
{
    public const string ResetDeliveryTimeSql = """
UPDATE hm_messages
SET
    messagenexttrytime = DATEADD(MINUTE, -1, SYSUTCDATETIME()),
    messagetype = 1
WHERE messageid = @MessageId
  AND messagetype IN (1, 3);

SELECT @@ROWCOUNT;
""";

    public const string RemoveSql = """
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @MessageFileName nvarchar(255);

SELECT @MessageFileName = messagefilename
FROM hm_messages WITH (UPDLOCK, READPAST, ROWLOCK)
WHERE messageid = @MessageId
  AND messagetype IN (1, 3)
  AND NOT
  (
      messagelocked = 1
      AND messageleaseowner IS NOT NULL
      AND messageleaseexpiresutc > SYSUTCDATETIME()
  );

IF @MessageFileName IS NOT NULL
BEGIN
    DELETE FROM hm_messagerecipients
    WHERE recipientmessageid = @MessageId;

    DELETE FROM hm_messages
    WHERE messageid = @MessageId
      AND messagetype IN (1, 3);

    IF @@ROWCOUNT <> 1
        SET @MessageFileName = NULL;
END;

COMMIT TRANSACTION;

SELECT @MessageFileName;
""";

    public const string ClearBatchSql = """
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Candidates TABLE
(
    MessageId bigint NOT NULL PRIMARY KEY
);

DECLARE @Removed TABLE
(
    MessageId bigint NOT NULL PRIMARY KEY,
    MessageFileName nvarchar(255) NOT NULL
);

INSERT INTO @Candidates (MessageId)
SELECT TOP (@BatchSize) messageid
FROM hm_messages WITH (UPDLOCK, READPAST, ROWLOCK)
WHERE messagetype = 1
  AND messagecreatetime <= @ClearStartedUtc
  AND NOT
  (
      messagelocked = 1
      AND messageleaseowner IS NOT NULL
      AND messageleaseexpiresutc > SYSUTCDATETIME()
  )
ORDER BY messageid;

DELETE recipients
FROM hm_messagerecipients AS recipients
INNER JOIN @Candidates AS candidates
    ON candidates.MessageId = recipients.recipientmessageid;

DELETE messages
OUTPUT
    deleted.messageid,
    deleted.messagefilename
INTO @Removed (MessageId, MessageFileName)
FROM hm_messages AS messages
INNER JOIN @Candidates AS candidates
    ON candidates.MessageId = messages.messageid
WHERE messages.messagetype = 1
  AND messages.messagecreatetime <= @ClearStartedUtc;

COMMIT TRANSACTION;

SELECT MessageFileName
FROM @Removed
ORDER BY MessageId;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly MessageFilePathResolver? _pathResolver;

    public SqlServerDeliveryQueueAdministrationStore(
        SqlServerConnectionFactory connectionFactory,
        MessageFilePathResolver? pathResolver = null)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
        _pathResolver = pathResolver;
    }

    public async ValueTask<bool> ResetDeliveryTimeAsync(
        long messageId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(ResetDeliveryTimeSql, connection);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;
        var affectedRows = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return affectedRows is int rows && rows == 1;
    }

    public async ValueTask<bool> RemoveAsync(
        long messageId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(RemoveSql, connection);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is not string messageFileName)
        {
            return false;
        }

        TryDeleteQueueFile(messageFileName);
        return true;
    }

    public async ValueTask<int> ClearBatchAsync(
        int batchSize,
        DateTime clearStartedUtc,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(ClearBatchSql, connection);
        command.Parameters.Add("@BatchSize", SqlDbType.Int).Value = batchSize;
        command.Parameters.Add("@ClearStartedUtc", SqlDbType.DateTime2).Value = clearStartedUtc;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var messageFileNames = new List<string>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            messageFileNames.Add(reader.GetString(0));
        }

        foreach (var messageFileName in messageFileNames)
        {
            TryDeleteQueueFile(messageFileName);
        }

        return messageFileNames.Count;
    }

    private void TryDeleteQueueFile(string messageFileName)
    {
        if (_pathResolver is null || string.IsNullOrWhiteSpace(messageFileName))
        {
            return;
        }

        try
        {
            var path = _pathResolver.Resolve(
                messageFileName,
                accountId: 0,
                folderId: 0,
                accountAddress: null);
            if (path is null)
            {
                return;
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (ArgumentException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }
}
