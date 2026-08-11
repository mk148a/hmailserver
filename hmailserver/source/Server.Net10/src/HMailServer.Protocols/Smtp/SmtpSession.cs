using System.Net;
using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Smtp;

public sealed class SmtpSession
{
    private static readonly Encoding ResponseEncoding = Encoding.ASCII;
    private static readonly Encoding ProtocolEncoding = Encoding.Latin1;
    private static readonly Encoding AuthEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly byte[] EmptyEventMessageData = "Subject: hMailServer event\r\n\r\n"u8.ToArray();
    private static readonly byte[] CrLfBytes = "\r\n"u8.ToArray();

    private readonly SmtpSessionOptions _options;
    private readonly ISmtpMessageReceiver? _messageReceiver;
    private readonly ISmtpRecipientValidator? _recipientValidator;
    private readonly IImapAccountAuthenticator? _accountAuthenticator;
    private readonly ISmtpEventScriptExecutor? _eventScriptExecutor;
    private readonly IClientAwareAuthenticationService? _clientAwareAuthenticationService;
    private long _nextSessionId;

    public SmtpSession(
        SmtpSessionOptions? options = null,
        ISmtpMessageReceiver? messageReceiver = null,
        ISmtpRecipientValidator? recipientValidator = null,
        IImapAccountAuthenticator? accountAuthenticator = null,
        ISmtpEventScriptExecutor? eventScriptExecutor = null,
        IAutoBanLogonFailureRecorder? autoBanLogonFailureRecorder = null,
        IClientAwareAuthenticationService? clientAwareAuthenticationService = null)
    {
        _options = options ?? new SmtpSessionOptions();
        _messageReceiver = messageReceiver;
        _recipientValidator = recipientValidator;
        _accountAuthenticator = accountAuthenticator;
        _eventScriptExecutor = eventScriptExecutor;
        _clientAwareAuthenticationService = clientAwareAuthenticationService
            ?? (accountAuthenticator is null
                ? null
                : new ClientAwareAuthenticationService(accountAuthenticator, autoBanLogonFailureRecorder));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.MaxLineBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(_options.MaxMessageBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(_options.MaximumIncorrectCommands);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.ServerName);
    }

    public async ValueTask RunAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        await RunAsync(
            stream,
            startTlsStreamProvider: null,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask RunAsync(
        Stream stream,
        ISmtpStartTlsStreamProvider? startTlsStreamProvider,
        CancellationToken cancellationToken)
    {
        await RunAsync(
            stream,
            startTlsStreamProvider,
            connectionContext: null,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask RunAsync(
        Stream stream,
        ISmtpStartTlsStreamProvider? startTlsStreamProvider,
        SmtpSessionConnectionContext? connectionContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        await WriteAsync(stream, GetGreeting(), cancellationToken).ConfigureAwait(false);

        var state = new SessionState(CreateConnectionContext(connectionContext));
        LineProtocolReader? reader = null;
        try
        {
            reader = CreateReader(stream);
            while (!cancellationToken.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidDataException ex)
                {
                    await WriteSmtpResponseAsync(
                        stream,
                        state,
                        $"500 {SanitizeResponseText(ex.Message)}",
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (line is null)
                {
                    return;
                }

                if (!TryParseCommand(line, out var command, out var arguments))
                {
                    await WriteSmtpResponseAsync(
                        stream,
                        state,
                        "500 Syntax error, command unrecognized",
                        cancellationToken).ConfigureAwait(false);
                    if (state.PendingDisconnect)
                    {
                        return;
                    }

                    continue;
                }

                var result = await DispatchAsync(
                    stream,
                    reader,
                    state,
                    startTlsStreamProvider,
                    command,
                    arguments,
                    cancellationToken).ConfigureAwait(false);
                if (result == SmtpDispatchResult.Close)
                {
                    return;
                }

                if (state.PendingDisconnect)
                {
                    return;
                }

                if (result == SmtpDispatchResult.UpgradeToTls)
                {
                    await reader.DisposeAsync().ConfigureAwait(false);
                    reader = null;

                    stream = await startTlsStreamProvider!
                        .UpgradeToTlsAsync(stream, cancellationToken)
                        .ConfigureAwait(false);
                    state.MarkSecureConnection();
                    reader = CreateReader(stream);
                }
            }
        }
        finally
        {
            if (reader is not null)
            {
                await reader.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private string GetGreeting()
    {
        var welcome = _options.GreetingProvider?.Invoke();
        if (welcome is null)
        {
            return IsSingleLineGreeting(_options.Greeting)
                ? _options.Greeting
                : $"220 {_options.ServerName} ESMTP\r\n";
        }

        if (welcome.Contains('\r') || welcome.Contains('\n'))
        {
            welcome = string.Empty;
        }

        var banner = string.IsNullOrEmpty(welcome)
            ? $"220 {_options.ServerName} ESMTP"
            : $"220 {welcome}{(welcome.EndsWith(" ESMTP", StringComparison.Ordinal) ? string.Empty : " ESMTP")}";
        return banner + "\r\n";
    }

    private static bool IsSingleLineGreeting(string greeting)
    {
        var terminatorIndex = greeting.IndexOf("\r\n", StringComparison.Ordinal);
        if (terminatorIndex < 0)
        {
            return !greeting.Contains('\r') && !greeting.Contains('\n');
        }

        return terminatorIndex == greeting.Length - 2 &&
               greeting.IndexOf('\r', terminatorIndex + 2) < 0 &&
               greeting.IndexOf('\n', terminatorIndex + 2) < 0;
    }

    private LineProtocolReader CreateReader(Stream stream) =>
        new(stream, _options.MaxLineBytes, ProtocolEncoding);

    private SmtpSessionConnectionContext CreateConnectionContext(SmtpSessionConnectionContext? context)
    {
        var sessionId = context?.SessionId ?? 0;
        if (sessionId <= 0)
        {
            sessionId = Interlocked.Increment(ref _nextSessionId);
        }

        return new SmtpSessionConnectionContext(
            context?.ClientIPAddress ?? string.Empty,
            context?.ClientPort ?? 0,
            sessionId);
    }

    private async ValueTask<SmtpDispatchResult> DispatchAsync(
        Stream stream,
        LineProtocolReader reader,
        SessionState state,
        ISmtpStartTlsStreamProvider? startTlsStreamProvider,
        string command,
        string arguments,
        CancellationToken cancellationToken)
    {
        switch (command)
        {
            case "EHLO":
                if (string.IsNullOrWhiteSpace(arguments))
                {
                    await WriteSmtpResponseAsync(stream, state, "501 Syntax: EHLO hostname", cancellationToken).ConfigureAwait(false);
                    return SmtpDispatchResult.Continue;
                }

                state.SetHelo(arguments, isExtendedSmtp: true);
                if (!await RunHeloEventAsync(stream, state, cancellationToken).ConfigureAwait(false))
                {
                    return SmtpDispatchResult.Continue;
                }

                await WriteAsync(
                    stream,
                    FormatEhloResponse(state, startTlsStreamProvider),
                    cancellationToken).ConfigureAwait(false);
                return SmtpDispatchResult.Continue;

            case "HELO":
                if (string.IsNullOrWhiteSpace(arguments))
                {
                    await WriteSmtpResponseAsync(stream, state, "501 Syntax: HELO hostname", cancellationToken).ConfigureAwait(false);
                    return SmtpDispatchResult.Continue;
                }

                state.SetHelo(arguments, isExtendedSmtp: false);
                if (!await RunHeloEventAsync(stream, state, cancellationToken).ConfigureAwait(false))
                {
                    return SmtpDispatchResult.Continue;
                }

                await WriteAsync(stream, $"250 {SanitizeResponseText(_options.ServerName)}\r\n", cancellationToken).ConfigureAwait(false);
                return SmtpDispatchResult.Continue;

            case "NOOP":
                await WriteAsync(stream, "250 OK\r\n", cancellationToken).ConfigureAwait(false);
                return SmtpDispatchResult.Continue;

            case "RSET":
                state.ResetTransaction();
                await WriteAsync(stream, "250 OK\r\n", cancellationToken).ConfigureAwait(false);
                return SmtpDispatchResult.Continue;

            case "AUTH":
                await HandleAuthAsync(stream, reader, state, arguments, cancellationToken).ConfigureAwait(false);
                return SmtpDispatchResult.Continue;

            case "STARTTLS":
                return await HandleStartTlsAsync(
                    stream,
                    state,
                    startTlsStreamProvider,
                    arguments,
                    cancellationToken).ConfigureAwait(false);

            case "MAIL":
                await HandleMailAsync(stream, state, arguments, cancellationToken).ConfigureAwait(false);
                return SmtpDispatchResult.Continue;

            case "RCPT":
                await HandleRecipientAsync(stream, state, arguments, cancellationToken).ConfigureAwait(false);
                return SmtpDispatchResult.Continue;

            case "DATA":
                return await HandleDataAsync(stream, reader, state, arguments, cancellationToken).ConfigureAwait(false);

            case "QUIT":
                await WriteAsync(stream, $"221 {SanitizeResponseText(_options.ServerName)} closing connection\r\n", cancellationToken).ConfigureAwait(false);
                return SmtpDispatchResult.Close;

            default:
                await WriteSmtpResponseAsync(stream, state, "502 Command not implemented", cancellationToken).ConfigureAwait(false);
                return SmtpDispatchResult.Continue;
        }
    }

    private async ValueTask<bool> RunHeloEventAsync(
        Stream stream,
        SessionState state,
        CancellationToken cancellationToken)
    {
        var result = ExecuteSmtpEvent(
            "OnHELO",
            state,
            SmtpEventScriptArgumentShape.ClientOnly,
            EmptyEventMessageData,
            cancellationToken);
        if (result.Accepted)
        {
            return true;
        }

        await WriteAsync(
            stream,
            NormalizeSmtpEventFailureResponse(result.FailureResponse) + "\r\n",
            cancellationToken).ConfigureAwait(false);
        return false;
    }

    private async ValueTask WriteSmtpResponseAsync(
        Stream stream,
        SessionState state,
        string response,
        CancellationToken cancellationToken)
    {
        var line = response.TrimEnd('\r', '\n');
        if (IsPermanentNegativeResponse(line))
        {
            state.InvalidCommandCount++;
            if (_options.DisconnectInvalidClients &&
                state.InvalidCommandCount > _options.MaximumIncorrectCommands)
            {
                await WriteAsync(stream, "Too many invalid commands. Bye!\r\n", cancellationToken).ConfigureAwait(false);
                ExecuteSmtpEvent(
                    "OnTooManyInvalidCommands",
                    state,
                    SmtpEventScriptArgumentShape.ClientAndMessage,
                    EmptyEventMessageData,
                    cancellationToken);
                state.RequestDisconnect();
                return;
            }
        }

        await WriteAsync(stream, line + "\r\n", cancellationToken).ConfigureAwait(false);
    }

    private static bool IsPermanentNegativeResponse(string response) =>
        response.Length >= 3 &&
        response[0] == '5' &&
        char.IsDigit(response[1]) &&
        char.IsDigit(response[2]);

    private SmtpRuleScriptExecutionResult ExecuteSmtpEvent(
        string eventName,
        SessionState state,
        SmtpEventScriptArgumentShape argumentShape,
        byte[] messageData,
        CancellationToken cancellationToken)
    {
        if (_eventScriptExecutor is null)
        {
            return SmtpRuleScriptExecutionResult.Continue(messageData);
        }

        try
        {
            return _eventScriptExecutor.Execute(
                new SmtpEventScriptExecutionRequest(
                    eventName,
                    CreateEventClient(state),
                    state.MailFrom ?? string.Empty,
                    state.Recipients,
                    messageData,
                    argumentShape),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return SmtpRuleScriptExecutionResult.Failure("451 Requested action aborted: local error in processing", messageData);
        }
    }

    private static SmtpEventScriptClient CreateEventClient(SessionState state) =>
        new(
            state.AuthenticatedAccount?.Address ?? string.Empty,
            state.ClientIPAddress,
            state.ClientPort,
            state.SessionId,
            state.HeloHost,
            state.AuthenticatedAccount is not null,
            state.IsSecureConnection);

    private static string NormalizeSmtpEventFailureResponse(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return "451 Requested action aborted: local error in processing";
        }

        var sanitized = SanitizeResponseText(response.Trim());
        if (sanitized.Length >= 4 &&
            char.IsDigit(sanitized[0]) &&
            char.IsDigit(sanitized[1]) &&
            char.IsDigit(sanitized[2]) &&
            sanitized[3] == ' ')
        {
            return sanitized;
        }

        return "451 Requested action aborted: local error in processing";
    }

    private async ValueTask HandleMailAsync(
        Stream stream,
        SessionState state,
        string arguments,
        CancellationToken cancellationToken)
    {
        if (!state.HasHelo)
        {
            await WriteSmtpResponseAsync(stream, state, "503 Send HELO/EHLO first", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!TryParsePathParameter(arguments, "FROM:", out var sender, out var remainingArguments))
        {
            await WriteSmtpResponseAsync(stream, state, "501 Syntax: MAIL FROM:<address>", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!TryParseMailParameters(remainingArguments, out var declaredSize, out var failureResponse))
        {
            await WriteSmtpResponseAsync(stream, state, failureResponse, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (declaredSize is { } size &&
            _options.MaxMessageBytes > 0 &&
            size > _options.MaxMessageBytes)
        {
            await WriteSmtpResponseAsync(stream, state, "552 Message size exceeds fixed maximum message size", cancellationToken).ConfigureAwait(false);
            return;
        }

        state.ResetTransaction();
        state.StartTransaction(sender, declaredSize);
        await WriteAsync(stream, "250 OK\r\n", cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<SmtpDispatchResult> HandleStartTlsAsync(
        Stream stream,
        SessionState state,
        ISmtpStartTlsStreamProvider? startTlsStreamProvider,
        string arguments,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            await WriteSmtpResponseAsync(stream, state, "501 Syntax: STARTTLS", cancellationToken).ConfigureAwait(false);
            return SmtpDispatchResult.Continue;
        }

        if (state.IsSecureConnection)
        {
            await WriteSmtpResponseAsync(stream, state, "503 TLS is already active", cancellationToken).ConfigureAwait(false);
            return SmtpDispatchResult.Continue;
        }

        if (startTlsStreamProvider is null || !startTlsStreamProvider.SupportsStartTls)
        {
            await WriteAsync(stream, "454 TLS not available\r\n", cancellationToken).ConfigureAwait(false);
            return SmtpDispatchResult.Continue;
        }

        if (state.MailFrom is not null || state.Recipients.Count > 0)
        {
            await WriteSmtpResponseAsync(stream, state, "503 Bad sequence of commands", cancellationToken).ConfigureAwait(false);
            return SmtpDispatchResult.Continue;
        }

        await WriteAsync(stream, "220 Ready to start TLS\r\n", cancellationToken).ConfigureAwait(false);
        return SmtpDispatchResult.UpgradeToTls;
    }

    private async ValueTask HandleAuthAsync(
        Stream stream,
        LineProtocolReader reader,
        SessionState state,
        string arguments,
        CancellationToken cancellationToken)
    {
        if (!state.HasHelo)
        {
            await WriteSmtpResponseAsync(stream, state, "503 Send EHLO/HELO first", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!IsAuthenticationAllowed(state))
        {
            await WriteSmtpResponseAsync(stream, state, "530 Must issue STARTTLS first", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (state.AuthenticatedAccount is not null)
        {
            await WriteSmtpResponseAsync(stream, state, "503 Already authenticated", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (_accountAuthenticator is null)
        {
            await WriteAsync(stream, "454 Temporary authentication failure\r\n", cancellationToken).ConfigureAwait(false);
            return;
        }

        var authParts = arguments.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (authParts.Length == 0)
        {
            await WriteSmtpResponseAsync(stream, state, "501 Syntax: AUTH mechanism [initial-response]", cancellationToken).ConfigureAwait(false);
            return;
        }

        var mechanism = authParts[0].ToUpperInvariant();
        switch (mechanism)
        {
            case "PLAIN":
                await HandleAuthPlainAsync(
                    stream,
                    reader,
                    state,
                    authParts.Length == 2 ? authParts[1] : null,
                    cancellationToken).ConfigureAwait(false);
                return;

            case "LOGIN":
                await HandleAuthLoginAsync(
                    stream,
                    reader,
                    state,
                    authParts.Length == 2 ? authParts[1] : null,
                    cancellationToken).ConfigureAwait(false);
                return;

            default:
                await WriteSmtpResponseAsync(stream, state, "504 Unrecognized authentication type", cancellationToken).ConfigureAwait(false);
                return;
        }
    }

    private async ValueTask HandleAuthPlainAsync(
        Stream stream,
        LineProtocolReader reader,
        SessionState state,
        string? initialResponse,
        CancellationToken cancellationToken)
    {
        var response = initialResponse;
        if (string.IsNullOrEmpty(response))
        {
            await WriteAsync(stream, "334 \r\n", cancellationToken).ConfigureAwait(false);
            response = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        }

        if (response is null)
        {
            return;
        }

        if (response.Equals("*", StringComparison.Ordinal))
        {
            await WriteSmtpResponseAsync(stream, state, "501 Authentication canceled", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!TryParsePlainCredentials(response, out var username, out var password, out var errorResponse))
        {
            await WriteSmtpResponseAsync(stream, state, errorResponse, cancellationToken).ConfigureAwait(false);
            return;
        }

        await AuthenticateAsync(stream, state, username, password, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask HandleAuthLoginAsync(
        Stream stream,
        LineProtocolReader reader,
        SessionState state,
        string? initialUsername,
        CancellationToken cancellationToken)
    {
        string? encodedUsername = initialUsername;
        if (string.IsNullOrEmpty(encodedUsername))
        {
            await WriteAsync(stream, "334 VXNlcm5hbWU6\r\n", cancellationToken).ConfigureAwait(false);
            encodedUsername = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        }

        if (encodedUsername is null)
        {
            return;
        }

        if (encodedUsername.Equals("*", StringComparison.Ordinal))
        {
            await WriteSmtpResponseAsync(stream, state, "501 Authentication canceled", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!TryDecodeAuthToken(encodedUsername, out var username, out var usernameError))
        {
            await WriteSmtpResponseAsync(stream, state, usernameError, cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteAsync(stream, "334 UGFzc3dvcmQ6\r\n", cancellationToken).ConfigureAwait(false);
        var encodedPassword = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (encodedPassword is null)
        {
            return;
        }

        if (encodedPassword.Equals("*", StringComparison.Ordinal))
        {
            await WriteSmtpResponseAsync(stream, state, "501 Authentication canceled", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!TryDecodeAuthToken(encodedPassword, out var password, out var passwordError))
        {
            await WriteSmtpResponseAsync(stream, state, passwordError, cancellationToken).ConfigureAwait(false);
            return;
        }

        await AuthenticateAsync(stream, state, username, password, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask AuthenticateAsync(
        Stream stream,
        SessionState state,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        if (_accountAuthenticator is null)
        {
            await WriteAsync(stream, "454 Temporary authentication failure\r\n", cancellationToken).ConfigureAwait(false);
            return;
        }

        var clientAuthentication = await _clientAwareAuthenticationService!
            .AuthenticateAsync(
                new ClientAuthenticationRequest(
                    username,
                    password,
                    IPAddress.TryParse(state.ClientIPAddress, out var clientAddress)
                        ? clientAddress
                        : null,
                    ClientAuthenticationCaller.Smtp),
                cancellationToken)
            .ConfigureAwait(false);
        var result = clientAuthentication.Authentication;
        RunClientLogonEvent(
            state,
            username,
            result.Succeeded && result.Account is not null,
            cancellationToken);
        if (!result.Succeeded || result.Account is null)
        {
            await WriteSmtpResponseAsync(stream, state, "535 Authentication failed", cancellationToken).ConfigureAwait(false);
            if (clientAuthentication.Disconnect)
            {
                state.RequestDisconnect();
            }

            return;
        }

        state.AuthenticatedAccount = result.Account;
        await WriteAsync(stream, "235 Authentication successful\r\n", cancellationToken).ConfigureAwait(false);
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
                        state.HeloHost,
                        isAuthenticated,
                        state.IsSecureConnection),
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

    private async ValueTask HandleRecipientAsync(
        Stream stream,
        SessionState state,
        string arguments,
        CancellationToken cancellationToken)
    {
        if (state.MailFrom is null)
        {
            await WriteSmtpResponseAsync(stream, state, "503 Need MAIL command", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!TryParsePathParameter(arguments, "TO:", out var recipient, out _))
        {
            await WriteSmtpResponseAsync(stream, state, "501 Syntax: RCPT TO:<address>", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(recipient))
        {
            await WriteSmtpResponseAsync(stream, state, "501 Recipient address is required", cancellationToken).ConfigureAwait(false);
            return;
        }

        var validation = await ValidateRecipientAsync(
            state.MailFrom,
            recipient,
            state.AuthenticatedAccount is not null,
            cancellationToken).ConfigureAwait(false);
        if (!validation.Accepted)
        {
            var response = string.IsNullOrWhiteSpace(validation.FailureResponse)
                ? "550 Recipient rejected"
                : SanitizeResponseText(validation.FailureResponse);
            if (IsUnknownRecipientResponse(response))
            {
                ExecuteSmtpEvent(
                    "OnRecipientUnknown",
                    state,
                    SmtpEventScriptArgumentShape.ClientAndMessage,
                    EmptyEventMessageData,
                    cancellationToken);
            }

            await WriteSmtpResponseAsync(stream, state, response, cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (var resolvedRecipient in validation.Recipients)
        {
            if (!state.Recipients.Any(existing =>
                    existing.Address.Equals(resolvedRecipient.Address, StringComparison.OrdinalIgnoreCase) &&
                    existing.LocalAccountId == resolvedRecipient.LocalAccountId))
            {
                state.Recipients.Add(resolvedRecipient);
            }
        }

        await WriteAsync(stream, "250 OK\r\n", cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<SmtpDispatchResult> HandleDataAsync(
        Stream stream,
        LineProtocolReader reader,
        SessionState state,
        string arguments,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            await WriteSmtpResponseAsync(stream, state, "501 Syntax: DATA", cancellationToken).ConfigureAwait(false);
            return SmtpDispatchResult.Continue;
        }

        if (state.MailFrom is null)
        {
            await WriteSmtpResponseAsync(stream, state, "503 Need MAIL command", cancellationToken).ConfigureAwait(false);
            return SmtpDispatchResult.Continue;
        }

        if (state.Recipients.Count == 0)
        {
            await WriteSmtpResponseAsync(stream, state, "503 Need RCPT command", cancellationToken).ConfigureAwait(false);
            return SmtpDispatchResult.Continue;
        }

        await WriteAsync(stream, "354 Start mail input; end with <CRLF>.<CRLF>\r\n", cancellationToken).ConfigureAwait(false);

        var data = await ReadMessageDataAsync(reader, cancellationToken).ConfigureAwait(false);
        if (data.Disconnected)
        {
            return SmtpDispatchResult.Close;
        }

        if (data.SizeExceeded)
        {
            state.ResetTransaction();
            await WriteSmtpResponseAsync(stream, state, "552 Message size exceeds fixed maximum message size", cancellationToken).ConfigureAwait(false);
            return SmtpDispatchResult.Continue;
        }

        var messageData = data.MessageData;
        var smtpDataEventResult = ExecuteSmtpEvent(
            "OnSMTPData",
            state,
            SmtpEventScriptArgumentShape.ClientAndMessage,
            messageData,
            cancellationToken);
        if (!smtpDataEventResult.Accepted)
        {
            state.ResetTransaction();
            await WriteAsync(
                stream,
                NormalizeSmtpEventFailureResponse(smtpDataEventResult.FailureResponse) + "\r\n",
                cancellationToken).ConfigureAwait(false);
            return SmtpDispatchResult.Continue;
        }

        if (smtpDataEventResult.DropMessage)
        {
            state.ResetTransaction();
            await WriteAsync(stream, "250 Queued\r\n", cancellationToken).ConfigureAwait(false);
            return SmtpDispatchResult.Continue;
        }

        if (smtpDataEventResult.MessageData is not null)
        {
            messageData = smtpDataEventResult.MessageData;
        }

        if (_messageReceiver is null)
        {
            state.ResetTransaction();
            await WriteAsync(stream, "451 Requested action aborted: local error in processing\r\n", cancellationToken).ConfigureAwait(false);
            return SmtpDispatchResult.Continue;
        }

        var request = new SmtpReceiveRequest(
            state.HeloHost,
            state.IsExtendedSmtp,
            state.MailFrom,
            state.Recipients.ToArray(),
            state.DeclaredSize,
            messageData,
            DateTimeOffset.UtcNow,
            ClientIPAddress: state.ClientIPAddress,
            ClientPort: state.ClientPort,
            SessionId: state.SessionId,
            AuthenticatedUsername: state.AuthenticatedAccount?.Address ?? string.Empty,
            IsAuthenticated: state.AuthenticatedAccount is not null,
            IsEncryptedConnection: state.IsSecureConnection);
        SmtpReceiveResult result;
        try
        {
            result = await _messageReceiver.ReceiveAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            state.ResetTransaction();
            await WriteAsync(stream, "451 Requested action aborted: local error in processing\r\n", cancellationToken).ConfigureAwait(false);
            return SmtpDispatchResult.Continue;
        }

        state.ResetTransaction();
        if (!result.Accepted)
        {
            var response = string.IsNullOrWhiteSpace(result.FailureResponse)
                ? "451 Requested action aborted: local error in processing"
                : SanitizeResponseText(result.FailureResponse);
            await WriteSmtpResponseAsync(stream, state, response, cancellationToken).ConfigureAwait(false);
            return SmtpDispatchResult.Continue;
        }

        await WriteAsync(stream, "250 Queued\r\n", cancellationToken).ConfigureAwait(false);
        return SmtpDispatchResult.Continue;
    }

    private async ValueTask<SmtpRecipientValidationResult> ValidateRecipientAsync(
        string mailFrom,
        string recipient,
        bool senderAuthenticated,
        CancellationToken cancellationToken)
    {
        if (_recipientValidator is null)
        {
            return SmtpRecipientValidationResult.Accept(
                new SmtpResolvedRecipient(recipient, recipient, LocalAccountId: 0, IsLocal: false));
        }

        try
        {
            return await _recipientValidator
                .ValidateAsync(
                    new SmtpRecipientValidationRequest(
                        mailFrom,
                        recipient,
                        senderAuthenticated),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return SmtpRecipientValidationResult.Reject("451 Requested action aborted: local error in processing");
        }
    }

    private static bool IsUnknownRecipientResponse(string response) =>
        response.Equals("550 Unknown user", StringComparison.OrdinalIgnoreCase) ||
        response.Equals("Unknown user", StringComparison.OrdinalIgnoreCase);

    private async ValueTask<SmtpDataReadResult> ReadMessageDataAsync(
        LineProtocolReader reader,
        CancellationToken cancellationToken)
    {
        await using var message = new MemoryStream();
        var totalBytes = 0L;
        var sizeExceeded = false;

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                return new SmtpDataReadResult([], totalBytes, SizeExceeded: sizeExceeded, Disconnected: true);
            }

            if (line.Equals(".", StringComparison.Ordinal))
            {
                return new SmtpDataReadResult(message.ToArray(), totalBytes, sizeExceeded, Disconnected: false);
            }

            if (line.StartsWith("..", StringComparison.Ordinal))
            {
                line = line[1..];
            }

            var lineBytes = ProtocolEncoding.GetBytes(line);
            totalBytes += lineBytes.Length + 2;
            if (_options.MaxMessageBytes > 0 && totalBytes > _options.MaxMessageBytes)
            {
                sizeExceeded = true;
                continue;
            }

            if (!sizeExceeded)
            {
                await message.WriteAsync(lineBytes, cancellationToken).ConfigureAwait(false);
                await message.WriteAsync(CrLfBytes, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private string FormatEhloResponse(
        SessionState state,
        ISmtpStartTlsStreamProvider? startTlsStreamProvider)
    {
        var builder = new StringBuilder();
        builder.Append("250-").Append(SanitizeResponseText(_options.ServerName)).Append("\r\n");
        if (_options.MaxMessageBytes > 0)
        {
            builder.Append("250-SIZE ").Append(_options.MaxMessageBytes).Append("\r\n");
        }

        if (startTlsStreamProvider?.SupportsStartTls == true && !state.IsSecureConnection)
        {
            builder.Append("250-STARTTLS\r\n");
        }

        if (_accountAuthenticator is not null && IsAuthenticationAllowed(state))
        {
            builder.Append("250-AUTH PLAIN LOGIN\r\n");
        }

        builder.Append("250 HELP\r\n");
        return builder.ToString();
    }

    private bool IsAuthenticationAllowed(SessionState state) =>
        state.IsSecureConnection || !_options.RequireTlsForAuthentication;

    private static bool TryParsePlainCredentials(
        string encodedCredentials,
        out string username,
        out string password,
        out string errorResponse)
    {
        username = string.Empty;
        password = string.Empty;

        if (!TryDecodeAuthBytes(encodedCredentials, out var decoded, out errorResponse))
        {
            return false;
        }

        var separator = Array.IndexOf(decoded, (byte)0) >= 0 ? (byte)0 : (byte)'\t';
        var firstSeparator = Array.IndexOf(decoded, separator);
        var secondSeparator = firstSeparator >= 0
            ? Array.IndexOf(decoded, separator, firstSeparator + 1)
            : -1;
        if (firstSeparator < 0 || secondSeparator < 0)
        {
            errorResponse = "501 Invalid AUTH PLAIN response";
            return false;
        }

        if (!TryDecodeAuthSegment(decoded, firstSeparator + 1, secondSeparator - firstSeparator - 1, out username, out errorResponse) ||
            !TryDecodeAuthSegment(decoded, secondSeparator + 1, decoded.Length - secondSeparator - 1, out password, out errorResponse))
        {
            return false;
        }

        if (username.Length == 0 || password.Length == 0)
        {
            errorResponse = "501 Invalid AUTH PLAIN response";
            return false;
        }

        return true;
    }

    private static bool TryDecodeAuthToken(
        string encodedValue,
        out string value,
        out string errorResponse)
    {
        value = string.Empty;
        if (!TryDecodeAuthBytes(encodedValue, out var decoded, out errorResponse))
        {
            return false;
        }

        return TryDecodeAuthSegment(decoded, 0, decoded.Length, out value, out errorResponse);
    }

    private static bool TryDecodeAuthBytes(
        string encodedValue,
        out byte[] decoded,
        out string errorResponse)
    {
        try
        {
            decoded = Convert.FromBase64String(encodedValue);
            errorResponse = string.Empty;
            return true;
        }
        catch (FormatException)
        {
            decoded = [];
            errorResponse = "501 Invalid base64 authentication token";
            return false;
        }
    }

    private static bool TryDecodeAuthSegment(
        byte[] decoded,
        int start,
        int length,
        out string value,
        out string errorResponse)
    {
        try
        {
            value = AuthEncoding.GetString(decoded.AsSpan(start, length));
            errorResponse = string.Empty;
            return true;
        }
        catch (DecoderFallbackException)
        {
            value = string.Empty;
            errorResponse = "501 Invalid UTF-8 authentication token";
            return false;
        }
    }

    private bool TryParseMailParameters(
        string arguments,
        out long? declaredSize,
        out string failureResponse)
    {
        declaredSize = null;
        failureResponse = string.Empty;

        foreach (var parameter in arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!parameter.StartsWith("SIZE=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!long.TryParse(
                    parameter.AsSpan("SIZE=".Length),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var size) ||
                size < 0)
            {
                failureResponse = "501 Syntax: MAIL FROM:<address> [SIZE=bytes]\r\n";
                return false;
            }

            declaredSize = size;
        }

        return true;
    }

    private static bool TryParsePathParameter(
        string arguments,
        string keyword,
        out string address,
        out string remainingArguments)
    {
        address = string.Empty;
        remainingArguments = string.Empty;

        var value = arguments.TrimStart();
        if (!value.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = value[keyword.Length..].TrimStart();
        if (value.Length == 0 || value[0] != '<')
        {
            return false;
        }

        var endIndex = value.IndexOf('>');
        if (endIndex < 0)
        {
            return false;
        }

        address = value[1..endIndex];
        remainingArguments = value[(endIndex + 1)..].Trim();
        return true;
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

    private static async ValueTask WriteAsync(
        Stream stream,
        string response,
        CancellationToken cancellationToken)
    {
        var bytes = ResponseEncoding.GetBytes(response);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static string SanitizeResponseText(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

    private sealed record SmtpDataReadResult(
        byte[] MessageData,
        long TotalBytes,
        bool SizeExceeded,
        bool Disconnected);

    private enum SmtpDispatchResult
    {
        Continue,
        Close,
        UpgradeToTls
    }

    private sealed class SessionState
    {
        public SessionState(SmtpSessionConnectionContext connectionContext)
        {
            ClientIPAddress = connectionContext.ClientIPAddress;
            ClientPort = connectionContext.ClientPort;
            SessionId = connectionContext.SessionId;
        }

        public string ClientIPAddress { get; }

        public int ClientPort { get; }

        public long SessionId { get; }

        public string HeloHost { get; private set; } = string.Empty;

        public bool IsExtendedSmtp { get; private set; }

        public bool HasHelo => HeloHost.Length > 0;

        public bool IsSecureConnection { get; private set; }

        public int InvalidCommandCount { get; set; }

        public bool PendingDisconnect { get; private set; }

        public string? MailFrom { get; private set; }

        public long? DeclaredSize { get; private set; }

        public List<SmtpResolvedRecipient> Recipients { get; } = [];

        public ImapAuthenticatedAccount? AuthenticatedAccount { get; set; }

        public void SetHelo(
            string heloHost,
            bool isExtendedSmtp)
        {
            HeloHost = heloHost;
            IsExtendedSmtp = isExtendedSmtp;
            ResetTransaction();
        }

        public void MarkSecureConnection()
        {
            IsSecureConnection = true;
            HeloHost = string.Empty;
            IsExtendedSmtp = false;
            AuthenticatedAccount = null;
            ResetTransaction();
        }

        public void StartTransaction(
            string mailFrom,
            long? declaredSize)
        {
            MailFrom = mailFrom;
            DeclaredSize = declaredSize;
            Recipients.Clear();
        }

        public void ResetTransaction()
        {
            MailFrom = null;
            DeclaredSize = null;
            Recipients.Clear();
        }

        public void RequestDisconnect()
        {
            PendingDisconnect = true;
        }
    }
}
