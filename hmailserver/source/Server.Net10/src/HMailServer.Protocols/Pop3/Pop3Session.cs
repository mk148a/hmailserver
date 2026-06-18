using System.Buffers;
using System.Globalization;
using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Pop3;

public sealed class Pop3Session
{
    private static readonly Encoding ResponseEncoding = Encoding.ASCII;
    private static readonly byte[] DotByte = "."u8.ToArray();
    private static readonly byte[] CrLfBytes = "\r\n"u8.ToArray();
    private static readonly byte[] LfByte = "\n"u8.ToArray();
    private static readonly byte[] MessageTerminatorBytes = ".\r\n"u8.ToArray();

    private readonly IImapAccountAuthenticator _accountAuthenticator;
    private readonly IPop3MailboxStore _mailboxStore;
    private readonly IPop3MailboxLockManager? _mailboxLockManager;
    private readonly Pop3SessionOptions _options;

    public Pop3Session(
        IImapAccountAuthenticator accountAuthenticator,
        IPop3MailboxStore mailboxStore,
        Pop3SessionOptions? options = null,
        IPop3MailboxLockManager? mailboxLockManager = null)
    {
        _accountAuthenticator = accountAuthenticator;
        _mailboxStore = mailboxStore;
        _mailboxLockManager = mailboxLockManager;
        _options = options ?? new Pop3SessionOptions();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.MaxLineBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.Greeting);
    }

    public async ValueTask RunAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        await WriteAsync(stream, _options.Greeting, cancellationToken).ConfigureAwait(false);

        var state = new SessionState();
        await using var reader = new LineProtocolReader(stream, _options.MaxLineBytes);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidDataException ex)
                {
                    await WriteErrAsync(stream, SanitizeResponseText(ex.Message), cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (line is null)
                {
                    return;
                }

                if (!TryParseCommand(line, out var command, out var arguments))
                {
                    await WriteErrAsync(stream, "Syntax error, command unrecognized", cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var shouldClose = await DispatchAsync(
                    stream,
                    state,
                    command,
                    arguments,
                    cancellationToken).ConfigureAwait(false);
                if (shouldClose)
                {
                    return;
                }
            }
        }
        finally
        {
            await state.ReleaseMailboxLockAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask<bool> DispatchAsync(
        Stream stream,
        SessionState state,
        string command,
        string arguments,
        CancellationToken cancellationToken)
    {
        switch (command)
        {
            case "USER":
                await HandleUserAsync(stream, state, arguments, cancellationToken).ConfigureAwait(false);
                return false;

            case "PASS":
                await HandlePassAsync(stream, state, arguments, cancellationToken).ConfigureAwait(false);
                return false;

            case "CAPA":
                await WriteAsync(stream, "+OK Capability list follows\r\nUIDL\r\nTOP\r\nUSER\r\n.\r\n", cancellationToken).ConfigureAwait(false);
                return false;

            case "STAT":
                if (!await RequireAuthenticatedAsync(stream, state, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                await HandleStatAsync(stream, state, cancellationToken).ConfigureAwait(false);
                return false;

            case "LIST":
                if (!await RequireAuthenticatedAsync(stream, state, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                await HandleListAsync(stream, state, arguments, cancellationToken).ConfigureAwait(false);
                return false;

            case "UIDL":
                if (!await RequireAuthenticatedAsync(stream, state, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                await HandleUidlAsync(stream, state, arguments, cancellationToken).ConfigureAwait(false);
                return false;

            case "RETR":
                if (!await RequireAuthenticatedAsync(stream, state, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                await HandleRetrAsync(stream, state, arguments, cancellationToken).ConfigureAwait(false);
                return false;

            case "TOP":
                if (!await RequireAuthenticatedAsync(stream, state, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                await HandleTopAsync(stream, state, arguments, cancellationToken).ConfigureAwait(false);
                return false;

            case "DELE":
                if (!await RequireAuthenticatedAsync(stream, state, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                await HandleDeleAsync(stream, state, arguments, cancellationToken).ConfigureAwait(false);
                return false;

            case "RSET":
                if (!await RequireAuthenticatedAsync(stream, state, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                state.ResetDeleted();
                await WriteOkAsync(stream, "Reset state", cancellationToken).ConfigureAwait(false);
                return false;

            case "NOOP":
                await WriteOkAsync(stream, string.Empty, cancellationToken).ConfigureAwait(false);
                return false;

            case "QUIT":
                await HandleQuitAsync(stream, state, cancellationToken).ConfigureAwait(false);
                return true;

            default:
                await WriteErrAsync(stream, "Unknown command", cancellationToken).ConfigureAwait(false);
                return false;
        }
    }

    private static async ValueTask HandleUserAsync(
        Stream stream,
        SessionState state,
        string arguments,
        CancellationToken cancellationToken)
    {
        if (state.IsAuthenticated)
        {
            await WriteErrAsync(stream, "Already authenticated", cancellationToken).ConfigureAwait(false);
            return;
        }

        var username = arguments.Trim();
        if (username.Length == 0)
        {
            await WriteErrAsync(stream, "Syntax: USER username", cancellationToken).ConfigureAwait(false);
            return;
        }

        state.PendingUsername = username;
        await WriteOkAsync(stream, "User accepted", cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask HandlePassAsync(
        Stream stream,
        SessionState state,
        string arguments,
        CancellationToken cancellationToken)
    {
        if (state.IsAuthenticated)
        {
            await WriteErrAsync(stream, "Already authenticated", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (state.PendingUsername is null)
        {
            await WriteErrAsync(stream, "USER required", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (arguments.Length == 0)
        {
            await WriteErrAsync(stream, "Syntax: PASS password", cancellationToken).ConfigureAwait(false);
            return;
        }

        var result = await _accountAuthenticator
            .AuthenticateAsync(state.PendingUsername, arguments, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded || result.Account is null)
        {
            await WriteErrAsync(
                stream,
                SanitizeResponseText(result.FailureMessage.Length == 0
                    ? "Invalid user name or password."
                    : result.FailureMessage),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        IAsyncDisposable? mailboxLock = null;
        if (_mailboxLockManager is not null)
        {
            mailboxLock = await _mailboxLockManager
                .TryAcquireAsync(result.Account, cancellationToken)
                .ConfigureAwait(false);
            if (mailboxLock is null)
            {
                await WriteErrAsync(stream, "Your mailbox is already locked", cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        try
        {
            var messages = await _mailboxStore
                .ListMessagesAsync(result.Account, cancellationToken)
                .ConfigureAwait(false);
            state.SetAuthenticated(result.Account, messages, mailboxLock);
            mailboxLock = null;
        }
        finally
        {
            if (mailboxLock is not null)
            {
                await mailboxLock.DisposeAsync().ConfigureAwait(false);
            }
        }

        await WriteOkAsync(stream, "Mailbox locked and ready", cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask HandleStatAsync(
        Stream stream,
        SessionState state,
        CancellationToken cancellationToken)
    {
        var count = 0;
        long size = 0;
        for (var i = 0; i < state.Messages.Count; i++)
        {
            if (state.IsDeleted(i + 1))
            {
                continue;
            }

            count++;
            size += state.Messages[i].Size;
        }

        await WriteOkAsync(stream, $"{count} {size}", cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask HandleListAsync(
        Stream stream,
        SessionState state,
        string arguments,
        CancellationToken cancellationToken)
    {
        if (arguments.Trim().Length == 0)
        {
            await WriteAsync(stream, "+OK Mailbox scan listing follows\r\n", cancellationToken).ConfigureAwait(false);
            for (var i = 0; i < state.Messages.Count; i++)
            {
                var listedSequenceNumber = i + 1;
                if (state.IsDeleted(listedSequenceNumber))
                {
                    continue;
                }

                await WriteAsync(stream, $"{listedSequenceNumber} {state.Messages[i].Size}\r\n", cancellationToken).ConfigureAwait(false);
            }

            await WriteAsync(stream, ".\r\n", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!TryGetMessageBySequence(state, arguments, out var sequenceNumber, out var message))
        {
            await WriteErrAsync(stream, "No such message", cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteOkAsync(stream, $"{sequenceNumber} {message.Size}", cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask HandleUidlAsync(
        Stream stream,
        SessionState state,
        string arguments,
        CancellationToken cancellationToken)
    {
        if (arguments.Trim().Length == 0)
        {
            await WriteAsync(stream, "+OK Unique-id listing follows\r\n", cancellationToken).ConfigureAwait(false);
            for (var i = 0; i < state.Messages.Count; i++)
            {
                var listedSequenceNumber = i + 1;
                if (state.IsDeleted(listedSequenceNumber))
                {
                    continue;
                }

                await WriteAsync(stream, $"{listedSequenceNumber} {SanitizeUid(state.Messages[i].Uid)}\r\n", cancellationToken).ConfigureAwait(false);
            }

            await WriteAsync(stream, ".\r\n", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!TryGetMessageBySequence(state, arguments, out var sequenceNumber, out var message))
        {
            await WriteErrAsync(stream, "No such message", cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteOkAsync(
            stream,
            $"{sequenceNumber} {SanitizeUid(message.Uid)}",
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask HandleRetrAsync(
        Stream stream,
        SessionState state,
        string arguments,
        CancellationToken cancellationToken)
    {
        if (!TryGetMessageBySequence(state, arguments, out _, out var message))
        {
            await WriteErrAsync(stream, "No such message", cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var messageStream = await _mailboxStore
            .OpenMessageAsync(state.Account!, message.MessageId, cancellationToken)
            .ConfigureAwait(false);
        await WriteOkAsync(stream, $"{message.Size} octets", cancellationToken).ConfigureAwait(false);
        await WriteDotStuffedMessageAsync(messageStream, stream, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask HandleTopAsync(
        Stream stream,
        SessionState state,
        string arguments,
        CancellationToken cancellationToken)
    {
        if (!TryParseTopArguments(arguments, out var sequenceNumber, out var bodyLineCount) ||
            !TryGetMessageBySequence(state, sequenceNumber, out var message))
        {
            await WriteErrAsync(stream, "No such message", cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var messageStream = await _mailboxStore
            .OpenMessageAsync(state.Account!, message.MessageId, cancellationToken)
            .ConfigureAwait(false);
        await WriteOkAsync(stream, $"{message.Size} octets", cancellationToken).ConfigureAwait(false);
        await WriteTopMessageAsync(messageStream, stream, bodyLineCount, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask HandleDeleAsync(
        Stream stream,
        SessionState state,
        string arguments,
        CancellationToken cancellationToken)
    {
        if (!TryGetMessageBySequence(state, arguments, out var sequenceNumber, out _))
        {
            await WriteErrAsync(stream, "No such message", cancellationToken).ConfigureAwait(false);
            return;
        }

        state.MarkDeleted(sequenceNumber);
        await WriteOkAsync(stream, "Message deleted", cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask HandleQuitAsync(
        Stream stream,
        SessionState state,
        CancellationToken cancellationToken)
    {
        if (state.IsAuthenticated)
        {
            var deletedMessageIds = state.GetDeletedMessageIds();
            if (deletedMessageIds.Count > 0)
            {
                await _mailboxStore
                    .DeleteMessagesAsync(state.Account!, deletedMessageIds, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        await WriteOkAsync(stream, "hMailServer POP3 server signing off", cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<bool> RequireAuthenticatedAsync(
        Stream stream,
        SessionState state,
        CancellationToken cancellationToken)
    {
        if (state.IsAuthenticated)
        {
            return true;
        }

        await WriteErrAsync(stream, "Authentication required", cancellationToken).ConfigureAwait(false);
        return false;
    }

    private static bool TryGetMessageBySequence(
        SessionState state,
        string arguments,
        out int sequenceNumber,
        out Pop3MessageListing message)
    {
        sequenceNumber = 0;
        message = default!;

        var trimmed = arguments.Trim();
        if (!int.TryParse(trimmed, out sequenceNumber) ||
            sequenceNumber <= 0 ||
            sequenceNumber > state.Messages.Count ||
            state.IsDeleted(sequenceNumber))
        {
            return false;
        }

        message = state.Messages[sequenceNumber - 1];
        return true;
    }

    private static bool TryGetMessageBySequence(
        SessionState state,
        int sequenceNumber,
        out Pop3MessageListing message)
    {
        message = default!;
        if (sequenceNumber <= 0 ||
            sequenceNumber > state.Messages.Count ||
            state.IsDeleted(sequenceNumber))
        {
            return false;
        }

        message = state.Messages[sequenceNumber - 1];
        return true;
    }

    private static async ValueTask WriteDotStuffedMessageAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        var atLineStart = true;
        var wroteAny = false;
        byte lastByte = 0;

        try
        {
            while (true)
            {
                var read = await source
                    .ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                wroteAny = true;
                var segmentStart = 0;
                for (var i = 0; i < read; i++)
                {
                    if (atLineStart && buffer[i] == (byte)'.')
                    {
                        if (i > segmentStart)
                        {
                            await destination
                                .WriteAsync(buffer.AsMemory(segmentStart, i - segmentStart), cancellationToken)
                                .ConfigureAwait(false);
                        }

                        await destination.WriteAsync(DotByte, cancellationToken).ConfigureAwait(false);
                        segmentStart = i;
                    }

                    atLineStart = buffer[i] == (byte)'\n';
                }

                if (read > segmentStart)
                {
                    await destination
                        .WriteAsync(buffer.AsMemory(segmentStart, read - segmentStart), cancellationToken)
                        .ConfigureAwait(false);
                }

                lastByte = buffer[read - 1];
            }

            if (wroteAny && lastByte != (byte)'\n')
            {
                await destination
                    .WriteAsync(lastByte == (byte)'\r' ? LfByte : CrLfBytes, cancellationToken)
                    .ConfigureAwait(false);
            }

            await destination.WriteAsync(MessageTerminatorBytes, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async ValueTask WriteTopMessageAsync(
        Stream source,
        Stream destination,
        int bodyLineCount,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            source,
            Encoding.Latin1,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: true);
        var headerComplete = false;
        var remainingBodyLines = bodyLineCount;

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (headerComplete)
            {
                if (remainingBodyLines <= 0)
                {
                    break;
                }

                remainingBodyLines--;
            }
            else if (line.Length == 0)
            {
                headerComplete = true;
            }

            if (line.StartsWith(".", StringComparison.Ordinal))
            {
                line = "." + line;
            }

            await WriteLatin1Async(destination, line, cancellationToken).ConfigureAwait(false);
            await destination.WriteAsync(CrLfBytes, cancellationToken).ConfigureAwait(false);
        }

        await destination.WriteAsync(MessageTerminatorBytes, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryParseTopArguments(
        string arguments,
        out int sequenceNumber,
        out int bodyLineCount)
    {
        sequenceNumber = 0;
        bodyLineCount = 0;

        var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 &&
            int.TryParse(parts[0], CultureInfo.InvariantCulture, out sequenceNumber) &&
            int.TryParse(parts[1], CultureInfo.InvariantCulture, out bodyLineCount) &&
            sequenceNumber > 0 &&
            bodyLineCount >= 0;
    }

    private static bool TryParseCommand(
        string line,
        out string command,
        out string arguments)
    {
        command = string.Empty;
        arguments = string.Empty;

        var trimmed = line.TrimStart();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var separatorIndex = trimmed.IndexOf(' ');
        if (separatorIndex < 0)
        {
            command = trimmed.ToUpperInvariant();
            return true;
        }

        command = trimmed[..separatorIndex].ToUpperInvariant();
        arguments = trimmed[(separatorIndex + 1)..].TrimStart();
        return command.Length > 0;
    }

    private static ValueTask WriteOkAsync(
        Stream stream,
        string message,
        CancellationToken cancellationToken) =>
        WriteAsync(
            stream,
            message.Length == 0 ? "+OK\r\n" : $"+OK {SanitizeResponseText(message)}\r\n",
            cancellationToken);

    private static ValueTask WriteErrAsync(
        Stream stream,
        string message,
        CancellationToken cancellationToken) =>
        WriteAsync(stream, $"-ERR {SanitizeResponseText(message)}\r\n", cancellationToken);

    private static async ValueTask WriteAsync(
        Stream stream,
        string response,
        CancellationToken cancellationToken)
    {
        var bytes = ResponseEncoding.GetBytes(response);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask WriteLatin1Async(
        Stream stream,
        string response,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.Latin1.GetBytes(response);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static string SanitizeUid(string uid) =>
        SanitizeResponseText(uid).Replace(" ", "_", StringComparison.Ordinal);

    private static string SanitizeResponseText(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

    private sealed class SessionState
    {
        private readonly HashSet<int> _deletedSequenceNumbers = new();
        private IAsyncDisposable? _mailboxLock;

        public string? PendingUsername { get; set; }

        public ImapAuthenticatedAccount? Account { get; private set; }

        public IReadOnlyList<Pop3MessageListing> Messages { get; private set; } = Array.Empty<Pop3MessageListing>();

        public bool IsAuthenticated => Account is not null;

        public void SetAuthenticated(
            ImapAuthenticatedAccount account,
            IReadOnlyList<Pop3MessageListing> messages,
            IAsyncDisposable? mailboxLock)
        {
            Account = account;
            Messages = messages;
            _mailboxLock = mailboxLock;
            _deletedSequenceNumbers.Clear();
        }

        public bool IsDeleted(int sequenceNumber) =>
            _deletedSequenceNumbers.Contains(sequenceNumber);

        public void MarkDeleted(int sequenceNumber) =>
            _deletedSequenceNumbers.Add(sequenceNumber);

        public void ResetDeleted() =>
            _deletedSequenceNumbers.Clear();

        public IReadOnlyCollection<long> GetDeletedMessageIds()
        {
            var messageIds = new List<long>(_deletedSequenceNumbers.Count);
            foreach (var sequenceNumber in _deletedSequenceNumbers.Order())
            {
                if (sequenceNumber > 0 && sequenceNumber <= Messages.Count)
                {
                    messageIds.Add(Messages[sequenceNumber - 1].MessageId);
                }
            }

            return messageIds;
        }

        public async ValueTask ReleaseMailboxLockAsync()
        {
            if (_mailboxLock is null)
            {
                return;
            }

            var mailboxLock = _mailboxLock;
            _mailboxLock = null;
            await mailboxLock.DisposeAsync().ConfigureAwait(false);
        }
    }
}
