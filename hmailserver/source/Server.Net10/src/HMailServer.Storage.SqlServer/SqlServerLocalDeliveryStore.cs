using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerLocalDeliveryStore : ILocalDeliveryStore
{
    public const string LoadAccountAddressSql = """
SELECT TOP (1) accountaddress
FROM hm_accounts
WHERE accountid = @AccountId
  AND accountactive <> 0;
""";

    public const string AllocateInboxUidSql = """
UPDATE hm_imapfolders WITH (UPDLOCK, ROWLOCK)
SET foldercurrentuid = foldercurrentuid + 1
OUTPUT INSERTED.folderid, INSERTED.foldercurrentuid
WHERE
    folderaccountid = @AccountId
    AND folderparentid = -1
    AND LOWER(foldername) = 'inbox';
""";

    public const string AllocateFolderUidSql = """
UPDATE hm_imapfolders WITH (UPDLOCK, ROWLOCK)
SET foldercurrentuid = foldercurrentuid + 1
OUTPUT INSERTED.folderid, INSERTED.foldercurrentuid
WHERE
    folderaccountid = @AccountId
    AND folderid = @FolderId;
""";

    public const string InsertDeliveredMessageSql = """
INSERT INTO hm_messages
(
    messageaccountid,
    messagefolderid,
    messagefilename,
    messagetype,
    messagefrom,
    messagesize,
    messagecurnooftries,
    messagenexttrytime,
    messageflags,
    messagecreatetime,
    messagelocked,
    messageuid
)
OUTPUT INSERTED.messageid
VALUES
(
    @AccountId,
    @FolderId,
    @MessageFileName,
    2,
    @MessageFrom,
    @MessageSize,
    0,
    CONVERT(datetime, '1901-01-01', 120),
    @MessageFlags,
    @MessageCreateTime,
    0,
    @MessageUid
);
""";

    public const string QueueDeliveredMessageForIndexingSql = """
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
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly MessageFilePathResolver _pathResolver;
    private readonly ISmtpAccountRuleProcessor? _accountRuleProcessor;
    private readonly IImapMailboxStore? _mailboxStore;
    private readonly SqlServerSmtpQueueWriter? _queueWriter;
    private readonly IScriptMessageCopyStore? _scriptMessageCopyStore;

    public SqlServerLocalDeliveryStore(
        SqlServerConnectionFactory connectionFactory,
        MessageFilePathResolver pathResolver,
        ISmtpAccountRuleProcessor? accountRuleProcessor = null,
        IImapMailboxStore? mailboxStore = null,
        SqlServerSmtpQueueWriter? queueWriter = null,
        IScriptMessageCopyStore? scriptMessageCopyStore = null)
    {
        _connectionFactory = connectionFactory;
        _pathResolver = pathResolver;
        _accountRuleProcessor = accountRuleProcessor;
        _mailboxStore = mailboxStore;
        _queueWriter = queueWriter;
        _scriptMessageCopyStore = scriptMessageCopyStore;
    }

    public async ValueTask<LocalDeliveryResult> DeliverAsync(
        DeliveryQueuedMessage message,
        DeliveryTargetBatch targetBatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(targetBatch);
        if (targetBatch.Target.Kind != DeliveryTargetKind.LocalAccount)
        {
            throw new InvalidOperationException("Local delivery requires a local account target.");
        }

        var accountId = targetBatch.Target.LocalAccountId;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountId);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var accountAddress = await LoadAccountAddressAsync(connection, accountId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(accountAddress))
        {
            throw new InvalidOperationException("Local delivery account could not be found.");
        }

        var sourcePath = _pathResolver.Resolve(
            message.FileName,
            accountId: 0,
            folderId: 0,
            accountAddress: null);
        if (sourcePath is null || !File.Exists(sourcePath))
        {
            throw new IOException("Queued message file could not be found.");
        }

        var destinationFileName = Guid.NewGuid().ToString("N") + ".eml";
        var destinationPath = _pathResolver.Resolve(
            destinationFileName,
            accountId,
            folderId: 0,
            accountAddress);
        if (destinationPath is null)
        {
            throw new IOException("Destination message path is invalid.");
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await CopyFileAsync(sourcePath, destinationPath, cancellationToken).ConfigureAwait(false);
        var deliveredMessageSize = message.Size;
        LocalDeliveryDestination? ruleDestination = null;

        if (_accountRuleProcessor is not null)
        {
            try
            {
                var accountRuleResult = await ApplyAccountRulesAsync(
                    message,
                    targetBatch,
                    accountId,
                    destinationPath,
                    cancellationToken).ConfigureAwait(false);
                await EnqueueGeneratedMessagesAsync(
                    accountRuleResult.GeneratedMessages ?? [],
                    message.CreatedUtc,
                    cancellationToken).ConfigureAwait(false);
                await CopyScriptRequestedMessagesAsync(
                    message,
                    accountId,
                    accountRuleResult.MessageCopyOperations ?? [],
                    cancellationToken).ConfigureAwait(false);
                if (accountRuleResult.DropMessage)
                {
                    TryDelete(destinationPath);
                    return new LocalDeliveryResult(
                        new MessageIdentity(0, accountId, 0, 0),
                        targetBatch.Recipients.Count);
                }

                deliveredMessageSize = accountRuleResult.MessageSize;
                ruleDestination = await ResolveRuleDestinationAsync(
                    accountId,
                    accountAddress,
                    destinationFileName,
                    destinationPath,
                    accountRuleResult.MoveToImapFolder,
                    cancellationToken).ConfigureAwait(false);
                if (ruleDestination is not null)
                {
                    destinationPath = ruleDestination.MessagePath;
                }
            }
            catch
            {
                TryDelete(destinationPath);
                throw;
            }
        }

        try
        {
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var deliveryAccountId = ruleDestination?.AccountId ?? accountId;
                var allocation = ruleDestination is null
                    ? await AllocateInboxUidAsync(connection, transaction, accountId, cancellationToken).ConfigureAwait(false)
                    : await AllocateFolderUidAsync(
                        connection,
                        transaction,
                        ruleDestination.AccountId,
                        ruleDestination.FolderId,
                        cancellationToken).ConfigureAwait(false);
                var messageId = await InsertDeliveredMessageAsync(
                    connection,
                    transaction,
                    message,
                    deliveryAccountId,
                    allocation.FolderId,
                    destinationFileName,
                    allocation.Uid,
                    deliveredMessageSize,
                    cancellationToken).ConfigureAwait(false);
                await QueueForIndexingAsync(connection, transaction, messageId, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                return new LocalDeliveryResult(
                    new MessageIdentity(messageId, accountId, allocation.FolderId, allocation.Uid),
                    targetBatch.Recipients.Count);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
        catch
        {
            TryDelete(destinationPath);
            throw;
        }
    }

    private async ValueTask<AccountRuleApplicationResult> ApplyAccountRulesAsync(
        DeliveryQueuedMessage message,
        DeliveryTargetBatch targetBatch,
        int accountId,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var messageData = await File.ReadAllBytesAsync(destinationPath, cancellationToken).ConfigureAwait(false);
        var recipients = targetBatch.Recipients
            .Select(static recipient => new SmtpResolvedRecipient(
                recipient.Address,
                recipient.OriginalAddress,
                recipient.LocalAccountId,
                IsLocal: true))
            .ToArray();
        var request = new SmtpReceiveRequest(
            HeloHost: string.Empty,
            IsExtendedSmtp: true,
            MailFrom: message.FromAddress,
            Recipients: recipients,
            DeclaredSize: message.Size,
            MessageData: messageData,
            ReceivedUtc: message.CreatedUtc,
            OriginalMessageSpamFlagged: (message.Flags & SmtpQueueWriteRequest.SpamFlag) != 0);

        var result = await _accountRuleProcessor!
            .ProcessAccountAsync(accountId, request, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Accepted)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.FailureResponse)
                    ? "Account rule processing failed."
                    : result.FailureResponse);
        }

        if (result.DropMessage)
        {
            return new AccountRuleApplicationResult(
                DropMessage: true,
                MessageSize: 0,
                GeneratedMessages: result.GeneratedMessages,
                MessageCopyOperations: result.MessageCopyOperations);
        }

        if (!ReferenceEquals(result.MessageData, messageData))
        {
            await File.WriteAllBytesAsync(destinationPath, result.MessageData, cancellationToken).ConfigureAwait(false);
        }

        return new AccountRuleApplicationResult(
            DropMessage: false,
            result.MessageData.LongLength,
            result.MoveToImapFolder,
            result.GeneratedMessages,
            result.MessageCopyOperations);
    }

    private async ValueTask CopyScriptRequestedMessagesAsync(
        DeliveryQueuedMessage message,
        int accountId,
        IReadOnlyList<ScriptMessageCopyOperation> operations,
        CancellationToken cancellationToken)
    {
        if (operations.Count == 0)
        {
            return;
        }

        if (_scriptMessageCopyStore is null)
        {
            throw new InvalidOperationException("Message.Copy requires a script message copy store.");
        }

        foreach (var operation in operations)
        {
            await _scriptMessageCopyStore.CopyAsync(
                new ScriptMessageCopyRequest(
                    accountId,
                    operation.DestinationFolderId,
                    message.FromAddress,
                    message.Flags,
                    message.CreatedUtc,
                    operation.MessageData),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask EnqueueGeneratedMessagesAsync(
        IReadOnlyList<SmtpRuleGeneratedMessage> generatedMessages,
        DateTimeOffset receivedUtc,
        CancellationToken cancellationToken)
    {
        if (_queueWriter is null || generatedMessages.Count == 0)
        {
            return;
        }

        foreach (var message in generatedMessages)
        {
            await _queueWriter
                .EnqueueAsync(
                    new SmtpQueueWriteRequest(
                        message.MailFrom,
                        message.Recipients,
                        message.MessageData,
                        receivedUtc,
                        MessageFlags: (byte)(SmtpQueueWriteRequest.RecentFlag
                            | (message.SpamFlagged ? SmtpQueueWriteRequest.SpamFlag : 0))),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask<LocalDeliveryDestination?> ResolveRuleDestinationAsync(
        int accountId,
        string accountAddress,
        string messageFileName,
        string currentPath,
        string? moveToImapFolder,
        CancellationToken cancellationToken)
    {
        if (_mailboxStore is null || string.IsNullOrWhiteSpace(moveToImapFolder))
        {
            return null;
        }

        var mailbox = await _mailboxStore
            .SelectMailboxAsync(accountId, moveToImapFolder, readOnly: false, cancellationToken)
            .ConfigureAwait(false);
        if (mailbox is null || mailbox.IsReadOnly)
        {
            return null;
        }

        var destinationAccountAddress = mailbox.AccountId == accountId ? accountAddress : null;
        var destinationPath = _pathResolver.Resolve(
            messageFileName,
            mailbox.AccountId,
            mailbox.FolderId,
            destinationAccountAddress);
        if (destinationPath is null)
        {
            return null;
        }

        if (!destinationPath.Equals(currentPath, StringComparison.OrdinalIgnoreCase))
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Move(currentPath, destinationPath);
        }

        return new LocalDeliveryDestination(mailbox.AccountId, mailbox.FolderId, destinationPath);
    }

    private static async ValueTask<string?> LoadAccountAddressAsync(
        SqlConnection connection,
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(LoadAccountAddressSql, connection);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result as string;
    }

    private static async ValueTask<InboxAllocation> AllocateInboxUidAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(AllocateInboxUidSql, connection, transaction);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Local delivery Inbox folder could not be found.");
        }

        return new InboxAllocation(reader.GetInt32(0), reader.GetInt64(1));
    }

    private static async ValueTask<InboxAllocation> AllocateFolderUidAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int accountId,
        int folderId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(AllocateFolderUidSql, connection, transaction);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@FolderId", SqlDbType.Int).Value = folderId;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Rule destination IMAP folder could not allocate a UID.");
        }

        return new InboxAllocation(reader.GetInt32(0), reader.GetInt64(1));
    }

    private static async ValueTask<long> InsertDeliveredMessageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        DeliveryQueuedMessage message,
        int accountId,
        int folderId,
        string messageFileName,
        long uid,
        long messageSize,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(InsertDeliveredMessageSql, connection, transaction);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@FolderId", SqlDbType.Int).Value = folderId;
        command.Parameters.Add("@MessageFileName", SqlDbType.NVarChar, 255).Value = messageFileName;
        command.Parameters.Add("@MessageFrom", SqlDbType.NVarChar, 255).Value = message.FromAddress;
        command.Parameters.Add("@MessageSize", SqlDbType.BigInt).Value = messageSize;
        command.Parameters.Add("@MessageFlags", SqlDbType.TinyInt).Value = (byte)(message.Flags | ImapMessageFlags.Recent);
        command.Parameters.Add("@MessageCreateTime", SqlDbType.DateTime).Value = message.CreatedUtc.UtcDateTime;
        command.Parameters.Add("@MessageUid", SqlDbType.BigInt).Value = uid;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async ValueTask QueueForIndexingAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long messageId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(QueueDeliveredMessageForIndexingSql, connection, transaction);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask CopyFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    private static void TryDelete(string path)
    {
        try
        {
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
    }

    private sealed record InboxAllocation(int FolderId, long Uid);

    private sealed record AccountRuleApplicationResult(
        bool DropMessage,
        long MessageSize,
        string? MoveToImapFolder = null,
        IReadOnlyList<SmtpRuleGeneratedMessage>? GeneratedMessages = null,
        IReadOnlyList<ScriptMessageCopyOperation>? MessageCopyOperations = null);

    private sealed record LocalDeliveryDestination(
        int AccountId,
        int FolderId,
        string MessagePath);
}
