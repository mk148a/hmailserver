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
