using System.Net;
using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols;

namespace HMailServer.Protocols.Imap;

public sealed class ImapSession
{
    private static readonly Encoding ResponseEncoding = Encoding.ASCII;
    private static readonly Encoding SaslPlainEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly byte[] EmptyEventMessageData = Array.Empty<byte>();

    private readonly ImapSearchCommandHandler _searchCommandHandler;
    private readonly ImapSortCommandHandler? _sortCommandHandler;
    private readonly ImapFetchCommandHandler? _fetchCommandHandler;
    private readonly ImapListCommandHandler? _listCommandHandler;
    private readonly ImapStatusCommandHandler? _statusCommandHandler;
    private readonly ImapStoreCommandHandler? _storeCommandHandler;
    private readonly ImapExpungeCommandHandler? _expungeCommandHandler;
    private readonly ImapCopyCommandHandler? _copyCommandHandler;
    private readonly ImapAppendCommandHandler? _appendCommandHandler;
    private readonly IImapIdleNotifier? _idleNotifier;
    private readonly ImapAclCommandHandler? _aclCommandHandler;
    private readonly ImapQuotaCommandHandler? _quotaCommandHandler;
    private readonly ImapSubscriptionCommandHandler? _subscriptionCommandHandler;
    private readonly ImapRenameCommandHandler? _renameCommandHandler;
    private readonly IImapRecentFlagStore? _recentFlagStore;
    private readonly ImapSessionOptions _options;
    private readonly IImapAccountAuthenticator? _accountAuthenticator;
    private readonly IImapMailboxStore? _mailboxStore;
    private readonly ISmtpEventScriptExecutor? _eventScriptExecutor;
    private readonly IClientAwareAuthenticationService? _clientAwareAuthenticationService;
    private readonly IImapFolderChangeTracker _folderChangeTracker;

    public ImapSession(
        ImapSearchCommandHandler searchCommandHandler,
        ImapSortCommandHandler? sortCommandHandler = null,
        ImapFetchCommandHandler? fetchCommandHandler = null,
        ImapListCommandHandler? listCommandHandler = null,
        ImapStatusCommandHandler? statusCommandHandler = null,
        ImapStoreCommandHandler? storeCommandHandler = null,
        ImapExpungeCommandHandler? expungeCommandHandler = null,
        ImapCopyCommandHandler? copyCommandHandler = null,
        ImapAppendCommandHandler? appendCommandHandler = null,
        IImapIdleNotifier? idleNotifier = null,
        ImapAclCommandHandler? aclCommandHandler = null,
        ImapQuotaCommandHandler? quotaCommandHandler = null,
        IImapRecentFlagStore? recentFlagStore = null,
        ImapSessionOptions? options = null,
        IImapAccountAuthenticator? accountAuthenticator = null,
        IImapMailboxStore? mailboxStore = null,
        ISmtpEventScriptExecutor? eventScriptExecutor = null,
        IAutoBanLogonFailureRecorder? autoBanLogonFailureRecorder = null,
        IClientAwareAuthenticationService? clientAwareAuthenticationService = null,
        ImapSubscriptionCommandHandler? subscriptionCommandHandler = null,
        ImapRenameCommandHandler? renameCommandHandler = null,
        IImapFolderChangeTracker? folderChangeTracker = null)
    {
        _searchCommandHandler = searchCommandHandler;
        _sortCommandHandler = sortCommandHandler;
        _fetchCommandHandler = fetchCommandHandler;
        _listCommandHandler = listCommandHandler;
        _statusCommandHandler = statusCommandHandler;
        _storeCommandHandler = storeCommandHandler;
        _expungeCommandHandler = expungeCommandHandler;
        _copyCommandHandler = copyCommandHandler;
        _appendCommandHandler = appendCommandHandler;
        _idleNotifier = idleNotifier;
        _aclCommandHandler = aclCommandHandler;
        _quotaCommandHandler = quotaCommandHandler;
        _subscriptionCommandHandler = subscriptionCommandHandler;
        _renameCommandHandler = renameCommandHandler;
        _recentFlagStore = recentFlagStore;
        _options = options ?? new ImapSessionOptions();
        _accountAuthenticator = accountAuthenticator;
        _mailboxStore = mailboxStore;
        _eventScriptExecutor = eventScriptExecutor;
        _clientAwareAuthenticationService = clientAwareAuthenticationService
            ?? (accountAuthenticator is null
                ? null
                : new ClientAwareAuthenticationService(accountAuthenticator, autoBanLogonFailureRecorder));
        _folderChangeTracker = folderChangeTracker ?? ImapFolderChangeTracker.Shared;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.MaxLineBytes);
    }

    public async ValueTask RunAsync(
        Stream stream,
        ImapSessionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(context);

        await WriteAsync(stream, _options.Greeting, cancellationToken).ConfigureAwait(false);

        var state = new SessionState(context);
        if (state.Account is { } account)
        {
            state.FolderChangeGeneration = _folderChangeTracker.GetGeneration(account.AccountId);
            state.AclChangeGeneration = state.SelectedMailbox is { } selected
                ? _folderChangeTracker.GetAclGeneration(selected.FolderId)
                : 0;
        }
        await using var reader = new LineProtocolReader(stream, _options.MaxLineBytes);
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidDataException ex)
            {
                await WriteAsync(stream, $"* BAD {SanitizeResponseText(ex.Message)}\r\n", cancellationToken).ConfigureAwait(false);
                return;
            }

            if (line is null)
            {
                return;
            }

            if (!ImapCommandLine.TryParse(line, out var commandLine))
            {
                await WriteAsync(stream, "* BAD Invalid command line\r\n", cancellationToken).ConfigureAwait(false);
                continue;
            }

            var shouldClose = await DispatchAsync(stream, reader, state, commandLine, cancellationToken).ConfigureAwait(false);
            if (shouldClose)
            {
                return;
            }
        }
    }

    private async ValueTask<bool> DispatchAsync(
        Stream stream,
        LineProtocolReader reader,
        SessionState state,
        ImapCommandLine commandLine,
        CancellationToken cancellationToken)
    {
        if (commandLine.IsUidCommand &&
            !commandLine.Command.Equals("SEARCH", StringComparison.Ordinal) &&
            !commandLine.Command.Equals("SORT", StringComparison.Ordinal) &&
            !commandLine.Command.Equals("FETCH", StringComparison.Ordinal) &&
            !commandLine.Command.Equals("STORE", StringComparison.Ordinal) &&
            !commandLine.Command.Equals("COPY", StringComparison.Ordinal) &&
            !commandLine.Command.Equals("MOVE", StringComparison.Ordinal))
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "BAD Unsupported UID command", cancellationToken).ConfigureAwait(false);
            return false;
        }

        await ApplyFolderChangeInvalidationAsync(state, cancellationToken).ConfigureAwait(false);

        switch (commandLine.Command)
        {
            case "CAPABILITY":
                await WriteAsync(
                    stream,
                    await FormatCapabilityResponseAsync(commandLine.Tag, state, cancellationToken).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
                return false;

            case "NOOP":
                await WriteTaggedAsync(stream, commandLine.Tag, "OK NOOP completed", cancellationToken).ConfigureAwait(false);
                return false;

            case "LOGIN":
                return await HandleLoginAsync(stream, state, commandLine, cancellationToken).ConfigureAwait(false);

            case "AUTHENTICATE":
                return await HandleAuthenticateAsync(stream, reader, state, commandLine, cancellationToken).ConfigureAwait(false);

            case "SELECT":
            case "EXAMINE":
                await HandleSelectAsync(
                    stream,
                    state,
                    commandLine,
                    readOnly: commandLine.Command.Equals("EXAMINE", StringComparison.Ordinal),
                    cancellationToken).ConfigureAwait(false);
                return false;

            case "LOGOUT":
                await WriteAsync(
                    stream,
                    $"* BYE hMailServer IMAP session closing\r\n{SanitizeAtom(commandLine.Tag)} OK LOGOUT completed\r\n",
                    cancellationToken).ConfigureAwait(false);
                return true;

            case "SEARCH":
                if (state.SelectedMailbox is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Select a mailbox first", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                var searchCommand = commandLine.IsUidCommand
                    ? $"UID SEARCH {commandLine.Arguments}"
                    : $"SEARCH {commandLine.Arguments}";
                var response = await _searchCommandHandler
                    .HandleAsync(
                        state.SelectedMailbox.AccountId,
                        state.SelectedMailbox.FolderId,
                        commandLine.Tag,
                        searchCommand,
                        cancellationToken,
                        state.RecentUids)
                    .ConfigureAwait(false);
                await WriteAsync(stream, response, cancellationToken).ConfigureAwait(false);
                return false;

            case "SORT":
                if (state.SelectedMailbox is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Select a mailbox first", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (_sortCommandHandler is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Sort backend is not configured", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                var sortResponse = await _sortCommandHandler
                    .HandleAsync(
                        state.SelectedMailbox.AccountId,
                        state.SelectedMailbox.FolderId,
                        commandLine.Tag,
                        commandLine.Arguments,
                        commandLine.IsUidCommand,
                        cancellationToken,
                        state.RecentUids)
                    .ConfigureAwait(false);
                await WriteAsync(stream, sortResponse, cancellationToken).ConfigureAwait(false);
                return false;

            case "IDLE":
                if (state.SelectedMailbox is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Select a mailbox first", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(commandLine.Arguments))
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "BAD IDLE does not accept arguments", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                return await HandleIdleAsync(stream, reader, state.SelectedMailbox, commandLine.Tag, cancellationToken).ConfigureAwait(false);

            case "GETACL":
            case "SETACL":
            case "DELETEACL":
            case "LISTRIGHTS":
            case "MYRIGHTS":
                return await HandleAclAsync(stream, state, commandLine, cancellationToken).ConfigureAwait(false);

            case "GETQUOTA":
            case "GETQUOTAROOT":
            case "SETQUOTA":
                return await HandleQuotaAsync(stream, state, commandLine, cancellationToken).ConfigureAwait(false);

            case "SUBSCRIBE":
            case "UNSUBSCRIBE":
                if (state.Account is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Authenticate first", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (_subscriptionCommandHandler is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Subscription backend is not configured", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                var subscriptionResponse = await _subscriptionCommandHandler
                    .HandleAsync(
                        state.Account.AccountId,
                        commandLine.Tag,
                        commandLine.Command,
                        commandLine.Arguments,
                        cancellationToken)
                    .ConfigureAwait(false);
                await WriteAsync(stream, subscriptionResponse, cancellationToken).ConfigureAwait(false);
                return false;

            case "RENAME":
                if (state.Account is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Authenticate first", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (_renameCommandHandler is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO RENAME backend is not configured", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                var renameResponse = await _renameCommandHandler
                    .HandleAsync(
                        state.Account.AccountId,
                        commandLine.Tag,
                        commandLine.Arguments,
                        cancellationToken)
                    .ConfigureAwait(false);
                await WriteAsync(stream, renameResponse, cancellationToken).ConfigureAwait(false);
                return false;

            case "FETCH":
                if (state.SelectedMailbox is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Select a mailbox first", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (_fetchCommandHandler is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Fetch backend is not configured", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                var fetchResponse = await _fetchCommandHandler
                    .HandleAsync(
                        state.SelectedMailbox.AccountId,
                        state.SelectedMailbox.FolderId,
                        commandLine.Tag,
                        commandLine.Arguments,
                        commandLine.IsUidCommand,
                        cancellationToken,
                        isReadOnly: state.SelectedMailbox.IsReadOnly,
                        aclRights: state.SelectedMailbox.AclRights)
                    .ConfigureAwait(false);
                await WriteAsync(stream, fetchResponse.AsMemory(), cancellationToken).ConfigureAwait(false);
                return false;

            case "LIST":
            case "LSUB":
                if (state.Account is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Authenticate first", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (_listCommandHandler is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Mailbox discovery backend is not configured", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                var listResponse = await _listCommandHandler
                    .HandleAsync(
                        state.Account.AccountId,
                        commandLine.Tag,
                        commandLine.Arguments,
                        subscribedOnly: commandLine.Command.Equals("LSUB", StringComparison.Ordinal),
                        cancellationToken)
                    .ConfigureAwait(false);
                await WriteAsync(stream, listResponse, cancellationToken).ConfigureAwait(false);
                return false;

            case "STATUS":
                if (state.Account is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Authenticate first", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (_statusCommandHandler is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Mailbox discovery backend is not configured", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                var statusResponse = await _statusCommandHandler
                    .HandleAsync(
                        state.Account.AccountId,
                        commandLine.Tag,
                        commandLine.Arguments,
                        cancellationToken)
                    .ConfigureAwait(false);
                await WriteAsync(stream, statusResponse, cancellationToken).ConfigureAwait(false);
                return false;

            case "STORE":
                if (state.SelectedMailbox is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Select a mailbox first", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (state.SelectedMailbox.IsReadOnly)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Store command on read-only folder", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (_storeCommandHandler is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Message mutation backend is not configured", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                var requiredStoreRights = _storeCommandHandler.GetRequiredAclRights(
                    state.SelectedMailbox.AccountId,
                    state.SelectedMailbox.FolderId,
                    commandLine.Arguments,
                    commandLine.IsUidCommand);
                if (!HasAclRights(state.SelectedMailbox, requiredStoreRights))
                {
                    if ((requiredStoreRights & ImapAclRights.WriteSeen) != 0 &&
                        !HasAclRights(state.SelectedMailbox, ImapAclRights.WriteSeen))
                    {
                        await WriteTaggedAsync(stream, commandLine.Tag, "NO ACL: WriteSeen permission denied (Required for STORE command).", cancellationToken).ConfigureAwait(false);
                    }
                    else if ((requiredStoreRights & ImapAclRights.WriteDeleted) != 0 &&
                             !HasAclRights(state.SelectedMailbox, ImapAclRights.WriteDeleted))
                    {
                        await WriteTaggedAsync(stream, commandLine.Tag, "NO ACL: DeleteMessages permission denied (Required for STORE command).", cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await WriteTaggedAsync(stream, commandLine.Tag, "NO ACL: WriteOthers permission denied (Required for STORE command).", cancellationToken).ConfigureAwait(false);
                    }

                    return false;
                }

                var storeResponse = await _storeCommandHandler
                    .HandleAsync(
                        state.SelectedMailbox.AccountId,
                        state.SelectedMailbox.FolderId,
                        commandLine.Tag,
                        commandLine.Arguments,
                        commandLine.IsUidCommand,
                        cancellationToken)
                    .ConfigureAwait(false);
                await WriteAsync(stream, storeResponse, cancellationToken).ConfigureAwait(false);
                return false;

            case "EXPUNGE":
                if (state.SelectedMailbox is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Select a mailbox first", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (state.SelectedMailbox.IsReadOnly)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Expunge command on read-only folder", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (_expungeCommandHandler is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Message mutation backend is not configured", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (!HasAclRights(state.SelectedMailbox, ImapAclRights.Expunge))
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO ACL: Expunge permission denied (Required for EXPUNGE command).", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                var expungeResponse = await _expungeCommandHandler
                    .HandleAsync(
                        state.SelectedMailbox.AccountId,
                        state.SelectedMailbox.FolderId,
                        commandLine.Tag,
                        cancellationToken)
                    .ConfigureAwait(false);
                await WriteAsync(stream, expungeResponse, cancellationToken).ConfigureAwait(false);
                return false;

            case "COPY":
            case "MOVE":
                if (state.Account is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Authenticate first", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (state.SelectedMailbox is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Select a mailbox first", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                var isMove = commandLine.Command.Equals("MOVE", StringComparison.Ordinal);
                if (isMove && state.SelectedMailbox.IsReadOnly)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Move command on read-only folder", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (_copyCommandHandler is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Message copy backend is not configured", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                var copyResult = await _copyCommandHandler
                    .ExecuteAsync(
                        state.Account.AccountId,
                        state.SelectedMailbox.AccountId,
                        state.SelectedMailbox.FolderId,
                        commandLine.Tag,
                        commandLine.Arguments,
                        commandLine.IsUidCommand,
                        deleteSource: isMove,
                        cancellationToken)
                    .ConfigureAwait(false);
                ApplyCopyRecentState(state, copyResult);
                await WriteAsync(stream, copyResult.Response, cancellationToken).ConfigureAwait(false);
                return false;

            case "APPEND":
                if (state.Account is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Authenticate first", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (_appendCommandHandler is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Message append backend is not configured", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                ImapAppendCommand appendCommand;
                try
                {
                    appendCommand = _appendCommandHandler.Parse(commandLine.Arguments);
                }
                catch (ImapAppendParseException ex)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, $"BAD {SanitizeResponseText(ex.Message)}", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                var appendDestination = await _appendCommandHandler
                    .ResolveDestinationAsync(state.Account.AccountId, appendCommand.MailboxName, cancellationToken)
                    .ConfigureAwait(false);
                if (appendDestination is null)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Can't find mailbox with that name.", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (appendDestination.IsReadOnly)
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO Destination mailbox is read-only.", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (!HasAclRights(appendDestination, ImapAclRights.Insert))
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "NO ACL: Insert permission denied (Required for APPEND command).", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                await WriteAsync(stream, "+ Ready for literal data\r\n", cancellationToken).ConfigureAwait(false);
                var literal = await reader.ReadExactAsync(appendCommand.LiteralByteCount, cancellationToken).ConfigureAwait(false);
                var terminator = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (terminator is not "")
                {
                    await WriteTaggedAsync(stream, commandLine.Tag, "BAD APPEND literal was not terminated with CRLF", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                var appendResult = await _appendCommandHandler
                    .ExecuteAsync(
                        state.Account.AccountId,
                        commandLine.Tag,
                        appendCommand,
                        literal,
                        cancellationToken)
                    .ConfigureAwait(false);
                ApplyAppendRecentState(state, appendResult);
                await WriteAsync(stream, appendResult.Response, cancellationToken).ConfigureAwait(false);
                return false;

            default:
                await WriteTaggedAsync(stream, commandLine.Tag, "BAD Unsupported command", cancellationToken).ConfigureAwait(false);
                return false;
        }
    }

    private async ValueTask<bool> HandleIdleAsync(
        Stream stream,
        LineProtocolReader reader,
        ImapMailboxSelection selectedMailbox,
        string tag,
        CancellationToken cancellationToken)
    {
        await WriteAsync(stream, "+ idling\r\n", cancellationToken).ConfigureAwait(false);

        using var idleCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var notificationTask = EmitIdleNotificationsAsync(stream, selectedMailbox, idleCancellation.Token).AsTask();

        string? terminator;
        try
        {
            terminator = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException ex)
        {
            await idleCancellation.CancelAsync().ConfigureAwait(false);
            await ObserveIdleNotificationsAsync(notificationTask).ConfigureAwait(false);
            await WriteAsync(stream, $"* BAD {SanitizeResponseText(ex.Message)}\r\n", cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            await idleCancellation.CancelAsync().ConfigureAwait(false);
        }

        await ObserveIdleNotificationsAsync(notificationTask).ConfigureAwait(false);

        if (terminator is null)
        {
            return true;
        }

        if (!terminator.Equals("DONE", StringComparison.OrdinalIgnoreCase))
        {
            await WriteTaggedAsync(stream, tag, "BAD Expected DONE to terminate IDLE", cancellationToken).ConfigureAwait(false);
            return false;
        }

                await WriteTaggedAsync(stream, tag, "OK IDLE completed", cancellationToken).ConfigureAwait(false);
        return false;
    }

    private async ValueTask<bool> HandleAclAsync(
        Stream stream,
        SessionState state,
        ImapCommandLine commandLine,
        CancellationToken cancellationToken)
    {
        if (state.Account is null)
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "NO Authenticate first", cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (_aclCommandHandler is null)
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "NO ACL backend is not configured", cancellationToken).ConfigureAwait(false);
            return false;
        }

        var response = await _aclCommandHandler
            .HandleAsync(
                state.Account.AccountId,
                commandLine.Tag,
                commandLine.Command,
                commandLine.Arguments,
                cancellationToken)
            .ConfigureAwait(false);
        await WriteAsync(stream, response, cancellationToken).ConfigureAwait(false);
        return false;
    }

    private async ValueTask<bool> HandleQuotaAsync(
        Stream stream,
        SessionState state,
        ImapCommandLine commandLine,
        CancellationToken cancellationToken)
    {
        if (state.Account is null)
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "NO Authenticate first", cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (_quotaCommandHandler is null)
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "NO QUOTA backend is not configured", cancellationToken).ConfigureAwait(false);
            return false;
        }

        var response = await _quotaCommandHandler
            .HandleAsync(
                state.Account.AccountId,
                commandLine.Tag,
                commandLine.Command,
                commandLine.Arguments,
                cancellationToken)
            .ConfigureAwait(false);
        await WriteAsync(stream, response, cancellationToken).ConfigureAwait(false);
        return false;
    }

    private static void ApplyAppendRecentState(
        SessionState state,
        ImapAppendCommandResult appendResult)
    {
        if (state.SelectedMailbox is null ||
            appendResult.AppendResult is null ||
            appendResult.DestinationAccountId != state.SelectedMailbox.AccountId ||
            appendResult.DestinationFolderId != state.SelectedMailbox.FolderId ||
            state.RecentUids is null)
        {
            return;
        }

        var recentUids = state.RecentUids.ToHashSet();
        recentUids.Add(appendResult.AppendResult.Identity.Uid);
        state.RecentUids = recentUids;
        state.SelectedMailbox = state.SelectedMailbox with
        {
            Exists = state.SelectedMailbox.Exists + 1,
            Recent = recentUids.Count,
            UidNext = Math.Max(state.SelectedMailbox.UidNext, appendResult.AppendResult.Identity.Uid + 1)
        };
    }

    private static void ApplyCopyRecentState(
        SessionState state,
        ImapCopyCommandResult copyResult)
    {
        if (state.SelectedMailbox is null || state.RecentUids is null || copyResult.Messages.Count == 0)
        {
            return;
        }

        var recentUids = state.RecentUids.ToHashSet();
        var exists = state.SelectedMailbox.Exists;
        var uidNext = state.SelectedMailbox.UidNext;

        if (copyResult.DestinationAccountId == state.SelectedMailbox.AccountId &&
            copyResult.DestinationFolderId == state.SelectedMailbox.FolderId)
        {
            foreach (var message in copyResult.Messages)
            {
                recentUids.Add(message.DestinationIdentity.Uid);
                exists++;
                uidNext = Math.Max(uidNext, message.DestinationIdentity.Uid + 1);
            }
        }

        if (copyResult.DeleteSource)
        {
            foreach (var message in copyResult.Messages)
            {
                if (message.SourceIdentity.AccountId == state.SelectedMailbox.AccountId &&
                    message.SourceIdentity.FolderId == state.SelectedMailbox.FolderId)
                {
                    recentUids.Remove(message.SourceIdentity.Uid);
                    exists = Math.Max(0, exists - 1);
                }
            }
        }

        state.RecentUids = recentUids;
        state.SelectedMailbox = state.SelectedMailbox with
        {
            Exists = exists,
            Recent = recentUids.Count,
            UidNext = uidNext
        };
    }

    private async ValueTask EmitIdleNotificationsAsync(
        Stream stream,
        ImapMailboxSelection selectedMailbox,
        CancellationToken cancellationToken)
    {
        if (_idleNotifier is null)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return;
        }

        var request = new ImapIdleWatchRequest(
            selectedMailbox.AccountId,
            selectedMailbox.FolderId,
            selectedMailbox.Name,
            selectedMailbox.Exists,
            selectedMailbox.Recent);

        await foreach (var idleEvent in _idleNotifier.WatchAsync(request, cancellationToken).ConfigureAwait(false))
        {
            await WriteAsync(stream, ImapIdleResponseFormatter.Format(idleEvent), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask ObserveIdleNotificationsAsync(Task notificationTask)
    {
        try
        {
            await notificationTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async ValueTask<bool> HandleLoginAsync(
        Stream stream,
        SessionState state,
        ImapCommandLine commandLine,
        CancellationToken cancellationToken)
    {
        if (state.Account is not null)
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "BAD Already authenticated", cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (!IsAuthenticationAllowed(state))
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "BAD A SSL/TLS-connection is required for authentication.", cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (_accountAuthenticator is null)
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "NO Authentication backend is not configured", cancellationToken).ConfigureAwait(false);
            return false;
        }

        IReadOnlyList<string> arguments;
        try
        {
            arguments = ImapCommandArguments.Parse(commandLine.Arguments);
        }
        catch (ImapSearchParseException ex)
        {
            await WriteTaggedAsync(stream, commandLine.Tag, $"BAD {SanitizeResponseText(ex.Message)}", cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (arguments.Count != 2)
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "BAD LOGIN requires username and password", cancellationToken).ConfigureAwait(false);
            return false;
        }

        return await AuthenticateAndSetStateAsync(
            stream,
            state,
            commandLine.Tag,
            arguments[0],
            arguments[1],
            "OK LOGIN completed",
            string.Empty,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<bool> HandleAuthenticateAsync(
        Stream stream,
        LineProtocolReader reader,
        SessionState state,
        ImapCommandLine commandLine,
        CancellationToken cancellationToken)
    {
        if (!await IsImapSaslPlainEnabledAsync(cancellationToken).ConfigureAwait(false))
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "NO IMAP AUTHENTICATE is not enabled.", cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (state.Account is not null)
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "BAD Already authenticated", cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (!IsAuthenticationAllowed(state))
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "BAD A SSL/TLS-connection is required for authentication.", cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (_accountAuthenticator is null)
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "NO Authentication backend is not configured", cancellationToken).ConfigureAwait(false);
            return false;
        }

        IReadOnlyList<string> arguments;
        try
        {
            arguments = ImapCommandArguments.Parse(commandLine.Arguments);
        }
        catch (ImapSearchParseException ex)
        {
            await WriteTaggedAsync(stream, commandLine.Tag, $"BAD {SanitizeResponseText(ex.Message)}", cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (arguments.Count is < 1 or > 2 ||
            !arguments[0].Equals("PLAIN", StringComparison.OrdinalIgnoreCase))
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "BAD Unsupported Authenticate mechanism", cancellationToken).ConfigureAwait(false);
            return false;
        }

        string saslResponse;
        if (arguments.Count == 2)
        {
            saslResponse = arguments[1];
        }
        else
        {
            await WriteAsync(stream, "+ \r\n", cancellationToken).ConfigureAwait(false);
            var continuation = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (continuation is null)
            {
                return true;
            }

            if (continuation.Equals("*", StringComparison.Ordinal))
            {
                await WriteTaggedAsync(stream, commandLine.Tag, "BAD AUTHENTICATE cancelled", cancellationToken).ConfigureAwait(false);
                return false;
            }

            saslResponse = continuation;
        }

        if (!TryParseSaslPlainResponse(
                saslResponse,
                out var authorizationId,
                out var username,
                out var password,
                out var errorMessage))
        {
            await WriteTaggedAsync(stream, commandLine.Tag, $"BAD {errorMessage}", cancellationToken).ConfigureAwait(false);
            return false;
        }

        return await AuthenticateAndSetStateAsync(
            stream,
            state,
            commandLine.Tag,
            username,
            password,
            "OK LOGIN completed",
            authorizationId,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<bool> AuthenticateAndSetStateAsync(
        Stream stream,
        SessionState state,
        string tag,
        string username,
        string password,
        string successResponse,
        string authorizationId,
        CancellationToken cancellationToken)
    {
        var clientAuthentication = await _clientAwareAuthenticationService!
            .AuthenticateAsync(
                new ClientAuthenticationRequest(
                    username,
                    password,
                    IPAddress.TryParse(state.ClientIPAddress, out var clientAddress)
                        ? clientAddress
                        : null,
                    ClientAuthenticationCaller.Imap,
                    authorizationId),
                cancellationToken)
            .ConfigureAwait(false);
        var result = clientAuthentication.Authentication;
        var isAuthenticated = result.Succeeded && result.Account is not null;
        RunClientLogonEvent(state, username, isAuthenticated, cancellationToken);

        if (!isAuthenticated)
        {
            var message = string.IsNullOrWhiteSpace(result.FailureMessage)
                ? "Invalid user name or password."
                : result.FailureMessage;
            var responseCode = result.IsProtocolError ? "BAD" : "NO";
            await WriteTaggedAsync(stream, tag, $"{responseCode} {SanitizeResponseText(message)}", cancellationToken).ConfigureAwait(false);
            return clientAuthentication.Disconnect;
        }

        var authenticatedAccount = result.Account
            ?? throw new InvalidOperationException("Successful IMAP authentication did not return an account.");
        state.Account = authenticatedAccount;
        state.SelectedMailbox = null;
        state.FolderChangeGeneration = _folderChangeTracker.GetGeneration(authenticatedAccount.AccountId);
        state.AclChangeGeneration = 0;
        await WriteTaggedAsync(stream, tag, successResponse, cancellationToken).ConfigureAwait(false);
        return false;
    }

    private async ValueTask ApplyFolderChangeInvalidationAsync(
        SessionState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (state.Account is not { } account)
        {
            return;
        }

        var changeAccountId = state.SelectedMailbox?.AccountId ?? account.AccountId;
        var generation = _folderChangeTracker.GetGeneration(changeAccountId);
        var previousGeneration = state.FolderChangeGeneration;
        if (generation == previousGeneration)
        {
            await ApplyAclChangeInvalidationAsync(state, cancellationToken).ConfigureAwait(false);
            return;
        }

        state.FolderChangeGeneration = generation;
        if (state.SelectedMailbox is not { } selectedMailbox
            || !_folderChangeTracker.TryGetLatestChange(
                changeAccountId,
                selectedMailbox.FolderId,
                out var change)
            || change.Generation <= previousGeneration)
        {
            await ApplyAclChangeInvalidationAsync(state, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (change.IsDeleted)
        {
            state.SelectedMailbox = null;
            state.RecentUids = null;
        }
        else if (change.Folder is { } folder
            && folder.AccountId == selectedMailbox.AccountId
            && folder.Id == selectedMailbox.FolderId)
        {
            state.SelectedMailbox = selectedMailbox with { Name = folder.Name };
        }

        await ApplyAclChangeInvalidationAsync(state, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ApplyAclChangeInvalidationAsync(
        SessionState state,
        CancellationToken cancellationToken)
    {
        if (state.Account is not { } account || state.SelectedMailbox is not { } selectedMailbox)
        {
            return;
        }

        var generation = _folderChangeTracker.GetAclGeneration(selectedMailbox.FolderId);
        if (generation > state.AclChangeGeneration)
        {
            state.AclChangeGeneration = generation;
        }

        if (_mailboxStore is not IImapSelectedMailboxAuthorization authorization)
        {
            return;
        }

        var refreshedMailbox = await authorization
            .RevalidateSelectedMailboxAsync(account.AccountId, selectedMailbox, cancellationToken)
            .ConfigureAwait(false);
        if (refreshedMailbox is null)
        {
            state.SelectedMailbox = null;
            state.RecentUids = null;
            return;
        }

        state.SelectedMailbox = selectedMailbox with
        {
            IsReadOnly = refreshedMailbox.IsReadOnly,
            AclRights = refreshedMailbox.AclRights
        };
    }

    private void RunClientLogonEvent(
        SessionState state,
        string username,
        bool isAuthenticated,
        CancellationToken cancellationToken)
    {
        if (_eventScriptExecutor is null)
        {
            return;
        }

        try
        {
            _eventScriptExecutor.Execute(
                new SmtpEventScriptExecutionRequest(
                    "OnClientLogon",
                    new SmtpEventScriptClient(
                        username,
                        state.ClientIPAddress,
                        state.ClientPort,
                        state.SessionId,
                        HeloHost: string.Empty,
                        IsAuthenticated: isAuthenticated,
                        IsEncryptedConnection: state.IsSecureConnection),
                    MailFrom: string.Empty,
                    Recipients: [],
                    EmptyEventMessageData,
                    SmtpEventScriptArgumentShape.ClientOnly),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
        }
    }

    private async ValueTask<string> FormatCapabilityResponseAsync(
        string tag,
        SessionState state,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder("* CAPABILITY IMAP4rev1 UIDPLUS SORT MOVE IDLE ACL QUOTA");
        if (IsAuthenticationAllowed(state))
        {
            if (await IsImapSaslPlainEnabledAsync(cancellationToken).ConfigureAwait(false))
            {
                builder.Append(" AUTH=PLAIN");
            }

            builder.Append(" SASL-IR");
        }

        builder.Append("\r\n")
            .Append(SanitizeAtom(tag))
            .Append(" OK CAPABILITY completed\r\n");
        return builder.ToString();
    }

    private ValueTask<bool> IsImapSaslPlainEnabledAsync(CancellationToken cancellationToken) =>
        _options.ImapSaslPlainEnabledProvider?.Invoke(cancellationToken)
        ?? ValueTask.FromResult(_options.ImapSaslPlainEnabled);

    private bool IsAuthenticationAllowed(SessionState state) =>
        state.IsSecureConnection || !_options.RequireTlsForAuthentication;

    private static bool TryParseSaslPlainResponse(
        string saslResponse,
        out string authorizationId,
        out string authenticationId,
        out string password,
        out string errorMessage)
    {
        authorizationId = string.Empty;
        authenticationId = string.Empty;
        password = string.Empty;
        errorMessage = string.Empty;

        byte[] decoded;
        try
        {
            decoded = saslResponse == "="
                ? []
                : Convert.FromBase64String(saslResponse);
        }
        catch (FormatException)
        {
            errorMessage = "Command has malformed base64 token.";
            return false;
        }

        var separator = Array.IndexOf(decoded, (byte)0) >= 0 ? (byte)0 : (byte)'\t';
        var firstSeparator = Array.IndexOf(decoded, separator);
        var secondSeparator = firstSeparator >= 0
            ? Array.IndexOf(decoded, separator, firstSeparator + 1)
            : -1;

        if (firstSeparator < 0 ||
            secondSeparator < 0 ||
            Array.IndexOf(decoded, separator, secondSeparator + 1) >= 0)
        {
            errorMessage = "Command has malformed base64 token.";
            return false;
        }

        if (!TryDecodeSaslSegment(decoded, 0, firstSeparator, out authorizationId, out errorMessage) ||
            !TryDecodeSaslSegment(decoded, firstSeparator + 1, secondSeparator - firstSeparator - 1, out authenticationId, out errorMessage) ||
            !TryDecodeSaslSegment(decoded, secondSeparator + 1, decoded.Length - secondSeparator - 1, out password, out errorMessage))
        {
            return false;
        }

        if (authenticationId.Length == 0)
        {
            errorMessage = "Command is missing username.";
            return false;
        }

        if (password.Length == 0)
        {
            errorMessage = "Command is missing password.";
            return false;
        }

        return true;
    }

    private static bool TryDecodeSaslSegment(
        byte[] decoded,
        int start,
        int length,
        out string value,
        out string errorMessage)
    {
        try
        {
            value = SaslPlainEncoding.GetString(decoded.AsSpan(start, length));
            errorMessage = string.Empty;
            return true;
        }
        catch (DecoderFallbackException)
        {
            value = string.Empty;
            errorMessage = "Command has malformed UTF-8 token.";
            return false;
        }
    }

    private async ValueTask HandleSelectAsync(
        Stream stream,
        SessionState state,
        ImapCommandLine commandLine,
        bool readOnly,
        CancellationToken cancellationToken)
    {
        if (state.Account is null)
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "NO Authenticate first", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (_mailboxStore is null)
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "NO Mailbox store is not configured", cancellationToken).ConfigureAwait(false);
            return;
        }

        IReadOnlyList<string> arguments;
        try
        {
            arguments = ImapCommandArguments.Parse(commandLine.Arguments);
        }
        catch (ImapSearchParseException ex)
        {
            await WriteTaggedAsync(stream, commandLine.Tag, $"BAD {SanitizeResponseText(ex.Message)}", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (arguments.Count != 1)
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "BAD SELECT requires one mailbox name", cancellationToken).ConfigureAwait(false);
            return;
        }

        var mailbox = await _mailboxStore
            .SelectMailboxAsync(state.Account.AccountId, arguments[0], readOnly, cancellationToken)
            .ConfigureAwait(false);
        if (mailbox is null)
        {
            await WriteTaggedAsync(stream, commandLine.Tag, "BAD Folder could not be found.", cancellationToken).ConfigureAwait(false);
            return;
        }

        mailbox = mailbox with { RequestedReadOnly = readOnly };

        var recentUids = await CaptureRecentUidsAsync(
            mailbox,
            clearRecentFlags: !readOnly && !mailbox.IsReadOnly,
            cancellationToken).ConfigureAwait(false);
        if (recentUids is not null)
        {
            mailbox = mailbox with { Recent = recentUids.Count };
            state.RecentUids = recentUids.ToHashSet();
        }
        else
        {
            state.RecentUids = null;
        }

        state.SelectedMailbox = mailbox;
        state.AclChangeGeneration = _folderChangeTracker.GetAclGeneration(mailbox.FolderId);
        await WriteAsync(stream, FormatSelectResponse(commandLine.Tag, commandLine.Command, mailbox), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IReadOnlyList<long>?> CaptureRecentUidsAsync(
        ImapMailboxSelection mailbox,
        bool clearRecentFlags,
        CancellationToken cancellationToken)
    {
        if (_recentFlagStore is null)
        {
            return null;
        }

        return await _recentFlagStore
            .CaptureRecentUidsAsync(
                mailbox.AccountId,
                mailbox.FolderId,
                clearRecentFlags,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static string FormatSelectResponse(
        string tag,
        string command,
        ImapMailboxSelection mailbox)
    {
        var builder = new StringBuilder();
        builder.Append("* ").Append(mailbox.Exists).Append(" EXISTS\r\n");
        builder.Append("* ").Append(mailbox.Recent).Append(" RECENT\r\n");
        builder.Append("* FLAGS (\\Deleted \\Seen \\Draft \\Answered \\Flagged)\r\n");
        builder.Append("* OK [UIDVALIDITY ").Append(mailbox.UidValidity).Append("] current uidvalidity\r\n");

        if (mailbox.FirstUnseenUid is { } firstUnseenUid)
        {
            builder.Append("* OK [UNSEEN ").Append(firstUnseenUid).Append("] unseen messages\r\n");
        }

        builder.Append("* OK [UIDNEXT ").Append(mailbox.UidNext).Append("] next uid\r\n");
        builder.Append("* OK [PERMANENTFLAGS (\\Deleted \\Seen \\Draft \\Answered \\Flagged)] limited\r\n");
        builder.Append(SanitizeAtom(tag))
            .Append(mailbox.IsReadOnly ? " OK [READ-ONLY] " : " OK [READ-WRITE] ")
            .Append(command)
            .Append(" completed\r\n");
        return builder.ToString();
    }

    private static ValueTask WriteTaggedAsync(
        Stream stream,
        string tag,
        string response,
        CancellationToken cancellationToken) =>
        WriteAsync(stream, $"{SanitizeAtom(tag)} {response}\r\n", cancellationToken);

    private static async ValueTask WriteAsync(
        Stream stream,
        string response,
        CancellationToken cancellationToken)
    {
        var bytes = ResponseEncoding.GetBytes(response);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask WriteAsync(
        Stream stream,
        ReadOnlyMemory<byte> response,
        CancellationToken cancellationToken)
    {
        await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static string SanitizeAtom(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

    private static bool HasAclRights(ImapMailboxSelection mailbox, long requiredRights) =>
        requiredRights == 0 || (mailbox.AclRights & requiredRights) == requiredRights;

    private static string SanitizeResponseText(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

    private sealed class SessionState
    {
        public SessionState(ImapSessionContext context)
        {
            IsSecureConnection = context.IsSecureConnection;
            ClientIPAddress = context.ClientIPAddress;
            ClientPort = context.ClientPort;
            SessionId = context.SessionId;

            if (context.AccountId is { } accountId)
            {
                Account = new ImapAuthenticatedAccount(accountId, context.AccountAddress ?? string.Empty);
            }

            if (context.AccountId is { } selectedAccountId && context.FolderId is { } selectedFolderId)
            {
                SelectedMailbox = new ImapMailboxSelection(
                    selectedAccountId,
                    selectedFolderId,
                    "INBOX",
                    Exists: 0,
                    Recent: 0,
                    UidValidity: 1,
                    UidNext: 1,
                    FirstUnseenUid: null,
                    IsReadOnly: false);
            }
        }

        public ImapAuthenticatedAccount? Account { get; set; }

        public ImapMailboxSelection? SelectedMailbox { get; set; }

        public IReadOnlySet<long>? RecentUids { get; set; }

        public long FolderChangeGeneration { get; set; }

        public long AclChangeGeneration { get; set; }

        public bool IsSecureConnection { get; }

        public string ClientIPAddress { get; }

        public int ClientPort { get; }

        public long SessionId { get; }
    }
}
