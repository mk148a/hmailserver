using System.Buffers;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Pop3;

public sealed class TcpExternalFetchSessionFactory : IExternalFetchSessionFactory
{
    private const int ControlLineBudgetBytes = 250_000;
    private static readonly Encoding CommandEncoding = Encoding.ASCII;
    private static readonly TimeSpan QuitTimeout = TimeSpan.FromSeconds(5);

    private readonly ExternalFetchPop3ClientOptions _options;
    private readonly IExternalFetchAddressResolver _addressResolver;
    private readonly Action<ExternalFetchEndpointDecision>? _endpointDecisionObserver;
    private readonly Func<Stream, string, CancellationToken, ValueTask<Stream>> _tlsStreamFactory;

    public TcpExternalFetchSessionFactory(
        ExternalFetchPop3ClientOptions? options = null,
        IExternalFetchAddressResolver? addressResolver = null,
        Action<ExternalFetchEndpointDecision>? endpointDecisionObserver = null,
        Func<Stream, string, CancellationToken, ValueTask<Stream>>? tlsStreamFactory = null)
    {
        _options = options ?? new ExternalFetchPop3ClientOptions();
        _addressResolver = addressResolver ?? new SystemExternalFetchAddressResolver();
        _endpointDecisionObserver = endpointDecisionObserver;
        _tlsStreamFactory = tlsStreamFactory ?? UpgradeToTlsAsync;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_options.OperationTimeout.Ticks, 0);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.ReceiveBufferBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.SendBufferBytes);
    }

    public async ValueTask<IExternalFetchSession> ConnectAsync(
        ExternalFetchAccountLease account,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.ServerType != ExternalFetchServerType.Pop3)
        {
            throw new NotSupportedException("Only POP3 external fetch accounts are supported.");
        }

        var client = new TcpClient
        {
            NoDelay = _options.NoDelay,
            ReceiveBufferSize = _options.ReceiveBufferBytes,
            SendBufferSize = _options.SendBufferBytes
        };

        try
        {
            var resolvedAddresses = await ExecuteOperationAsync(
                    token => _addressResolver.ResolveAddressesAsync(account.ServerAddress, token),
                    cancellationToken)
                .ConfigureAwait(false);
            var decision = ExternalFetchEndpointPolicy.Evaluate(
                account.ServerAddress,
                resolvedAddresses,
                _options.AllowedPrivateCidrs);
            _endpointDecisionObserver?.Invoke(decision);
            if (_options.EnforceEgressPolicy && !decision.IsAllowed)
            {
                throw new InvalidOperationException(
                    "External fetch destination was denied by the egress policy: " + decision.Reason + ".");
            }

            await ExecuteOperationAsync(
                    token => client.ConnectAsync(decision.Endpoint, account.ServerPort, token),
                    cancellationToken)
                .ConfigureAwait(false);
            Stream stream = client.GetStream();
            if (account.ConnectionSecurity == ExternalFetchConnectionSecurity.Ssl)
            {
                stream = await ExecuteOperationAsync(
                        token => _tlsStreamFactory(stream, account.ServerAddress, token),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var session = new TcpExternalFetchSession(
                client,
                stream,
                _tlsStreamFactory,
                _options.OperationTimeout);
            await session.InitializeAsync(account, cancellationToken).ConfigureAwait(false);
            return session;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private async ValueTask ExecuteOperationAsync(
        Func<CancellationToken, ValueTask> operation,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.OperationTimeout);
        try
        {
            await operation(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("External POP3 operation timed out.", ex);
        }
    }

    private async ValueTask<T> ExecuteOperationAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.OperationTimeout);
        try
        {
            return await operation(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("External POP3 operation timed out.", ex);
        }
    }

    private static async ValueTask<Stream> UpgradeToTlsAsync(
        Stream stream,
        string targetHost,
        CancellationToken cancellationToken)
    {
        var sslStream = new SslStream(stream, leaveInnerStreamOpen: false);
        try
        {
            await sslStream.AuthenticateAsClientAsync(
                new SslClientAuthenticationOptions { TargetHost = targetHost },
                cancellationToken).ConfigureAwait(false);
            return sslStream;
        }
        catch
        {
            await sslStream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private sealed class TcpExternalFetchSession : IExternalFetchSession
    {
        private static readonly byte[] CrLf = "\r\n"u8.ToArray();

        private readonly TcpClient _client;
        private readonly Func<Stream, string, CancellationToken, ValueTask<Stream>> _tlsStreamFactory;
        private readonly TimeSpan _operationTimeout;
        private Stream _stream;
        private Pop3LineReader _reader;
        private bool _quitSent;

        public TcpExternalFetchSession(
            TcpClient client,
            Stream stream,
            Func<Stream, string, CancellationToken, ValueTask<Stream>> tlsStreamFactory,
            TimeSpan operationTimeout)
        {
            _client = client;
            _tlsStreamFactory = tlsStreamFactory;
            _operationTimeout = operationTimeout;
            _stream = stream;
            _reader = new Pop3LineReader(stream);
        }

        public async ValueTask InitializeAsync(
            ExternalFetchAccountLease account,
            CancellationToken cancellationToken)
        {
            await ReadOkLineAsync(cancellationToken).ConfigureAwait(false);

            if (account.ConnectionSecurity is ExternalFetchConnectionSecurity.StartTlsOptional or ExternalFetchConnectionSecurity.StartTlsRequired)
            {
                var supportsStartTls = await ReadStartTlsCapabilityAsync(cancellationToken).ConfigureAwait(false);
                if (supportsStartTls)
                {
                    var startTlsResponse = await SendCommandReadLineAsync("STLS", cancellationToken).ConfigureAwait(false);
                    if (!IsOk(startTlsResponse))
                    {
                        throw new InvalidOperationException("External POP3 server rejected STLS.");
                    }

                    _stream = await ExecuteOperationAsync(
                            token => _tlsStreamFactory(_stream, account.ServerAddress, token),
                            cancellationToken)
                        .ConfigureAwait(false);
                    _reader = new Pop3LineReader(_stream);
                }
                else if (account.ConnectionSecurity == ExternalFetchConnectionSecurity.StartTlsRequired)
                {
                    throw new InvalidOperationException("External POP3 server does not advertise required STLS.");
                }
            }

            await SendCommandExpectOkAsync("USER " + account.Username, cancellationToken).ConfigureAwait(false);
            await SendCommandExpectOkAsync("PASS " + account.Password, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<IReadOnlyList<ExternalFetchRemoteMessage>> ListMessagesAsync(
            CancellationToken cancellationToken)
        {
            await SendCommandExpectOkAsync("UIDL", cancellationToken).ConfigureAwait(false);

            var messages = new List<ExternalFetchRemoteMessage>();
            while (true)
            {
                var lineBytes = await ReadDataLineBytesAsync(cancellationToken).ConfigureAwait(false);
                if (IsTerminator(lineBytes))
                {
                    break;
                }

                var line = CommandEncoding.GetString(lineBytes);
                var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var sequenceNumber) ||
                    string.IsNullOrWhiteSpace(parts[1]))
                {
                    continue;
                }

                messages.Add(new ExternalFetchRemoteMessage(sequenceNumber, parts[1]));
            }

            return messages;
        }

        public async ValueTask<byte[]> DownloadMessageAsync(
            ExternalFetchRemoteMessage message,
            CancellationToken cancellationToken)
        {
            await SendCommandExpectOkAsync(
                "RETR " + message.SequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                cancellationToken).ConfigureAwait(false);

            using var output = new MemoryStream();
            while (true)
            {
                var line = await ReadDataLineBytesAsync(cancellationToken).ConfigureAwait(false);
                if (IsTerminator(line))
                {
                    break;
                }

                var span = line.AsSpan();
                if (span.Length > 1 && span[0] == (byte)'.')
                {
                    span = span[1..];
                }

                output.Write(span);
                output.Write(CrLf);
            }

            return output.ToArray();
        }

        public async ValueTask DeleteMessageAsync(
            ExternalFetchRemoteMessage message,
            CancellationToken cancellationToken)
        {
            // Legacy cleanup advances and drops the tracked UID after any DELE response.
            await SendCommandReadLineAsync(
                    "DELE " + message.SequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!_quitSent)
                {
                    _quitSent = true;
                    var timeout = _operationTimeout < QuitTimeout ? _operationTimeout : QuitTimeout;
                    await SendCommandExpectOkAsync("QUIT", CancellationToken.None, timeout).ConfigureAwait(false);
                }
            }
            catch (IOException)
            {
            }
            catch (SocketException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (TimeoutException)
            {
            }
            finally
            {
                await _stream.DisposeAsync().ConfigureAwait(false);
                _client.Dispose();
            }
        }

        private async ValueTask SendCommandExpectOkAsync(
            string command,
            CancellationToken cancellationToken,
            TimeSpan? operationTimeout = null)
        {
            var response = await SendCommandReadLineAsync(command, cancellationToken, operationTimeout).ConfigureAwait(false);
            if (!IsOk(response))
            {
                throw new InvalidOperationException("External POP3 command failed: " + SanitizeResponse(response));
            }
        }

        private async ValueTask<string> SendCommandReadLineAsync(
            string command,
            CancellationToken cancellationToken,
            TimeSpan? operationTimeout = null)
        {
            return await ExecuteOperationAsync(
                    async token =>
                    {
                        await SendCommandAsync(command, token).ConfigureAwait(false);
                        return await _reader.ReadControlLineAsync(token).ConfigureAwait(false);
                    },
                    cancellationToken,
                    operationTimeout)
                .ConfigureAwait(false);
        }

        private async ValueTask<bool> ReadStartTlsCapabilityAsync(CancellationToken cancellationToken)
        {
            var response = await SendCommandReadLineAsync("CAPA", cancellationToken).ConfigureAwait(false);
            if (!IsOk(response))
            {
                return false;
            }

            var supportsStartTls = false;
            while (true)
            {
                var lineBytes = await ReadControlLineBytesAsync(cancellationToken).ConfigureAwait(false);
                if (IsTerminator(lineBytes))
                {
                    return supportsStartTls;
                }

                var line = CommandEncoding.GetString(lineBytes).Trim();
                if (line.Equals("STLS", StringComparison.OrdinalIgnoreCase))
                {
                    supportsStartTls = true;
                }
            }
        }

        private async ValueTask ReadOkLineAsync(CancellationToken cancellationToken)
        {
            var line = await ReadControlLineAsync(cancellationToken).ConfigureAwait(false);
            if (!IsOk(line))
            {
                throw new InvalidOperationException("External POP3 server rejected the connection: " + SanitizeResponse(line));
            }
        }

        private async ValueTask SendCommandAsync(
            string command,
            CancellationToken cancellationToken)
        {
            var bytes = CommandEncoding.GetBytes(command + "\r\n");
            await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private ValueTask<string> ReadControlLineAsync(CancellationToken cancellationToken) =>
            ExecuteOperationAsync(
                token => _reader.ReadControlLineAsync(token),
                cancellationToken);

        private ValueTask<byte[]> ReadControlLineBytesAsync(CancellationToken cancellationToken) =>
            ExecuteOperationAsync(
                token => _reader.ReadControlLineBytesAsync(token),
                cancellationToken);

        private ValueTask<byte[]> ReadDataLineBytesAsync(CancellationToken cancellationToken) =>
            ExecuteOperationAsync(
                token => _reader.ReadDataLineBytesAsync(token),
                cancellationToken);

        private async ValueTask<T> ExecuteOperationAsync<T>(
            Func<CancellationToken, ValueTask<T>> operation,
            CancellationToken cancellationToken,
            TimeSpan? operationTimeout = null)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(operationTimeout ?? _operationTimeout);
            try
            {
                return await operation(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("External POP3 operation timed out.", ex);
            }
        }

        private static bool IsOk(string response) =>
            response.StartsWith("+OK", StringComparison.OrdinalIgnoreCase);

        private static bool IsTerminator(ReadOnlySpan<byte> line) =>
            line.Length == 1 && line[0] == (byte)'.';

        private static string SanitizeResponse(string response) =>
            response.Replace('\r', ' ').Replace('\n', ' ');
    }

    private sealed class Pop3LineReader
    {
        private readonly Stream _stream;
        private readonly byte[] _singleByte = new byte[1];

        public Pop3LineReader(Stream stream)
        {
            _stream = stream;
        }

        public async ValueTask<string> ReadControlLineAsync(CancellationToken cancellationToken)
        {
            var line = await ReadControlLineBytesAsync(cancellationToken).ConfigureAwait(false);
            return CommandEncoding.GetString(line);
        }

        public ValueTask<byte[]> ReadControlLineBytesAsync(CancellationToken cancellationToken) =>
            ReadLineBytesAsync(requireCrlf: true, ControlLineBudgetBytes, cancellationToken);

        public ValueTask<byte[]> ReadDataLineBytesAsync(CancellationToken cancellationToken) =>
            ReadLineBytesAsync(requireCrlf: false, maxBytes: null, cancellationToken);

        private async ValueTask<byte[]> ReadLineBytesAsync(
            bool requireCrlf,
            int? maxBytes,
            CancellationToken cancellationToken)
        {
            using var output = new MemoryStream();
            while (true)
            {
                var read = await _stream.ReadAsync(_singleByte, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new IOException("External POP3 server closed the connection.");
                }

                if (_singleByte[0] == (byte)'\n')
                {
                    if (requireCrlf && (output.Length == 0 || output.GetBuffer()[(int)output.Length - 1] != (byte)'\r'))
                    {
                        throw new IOException("External POP3 control line was not terminated with CRLF.");
                    }

                    break;
                }

                output.WriteByte(_singleByte[0]);
                if (maxBytes is not null && output.Length >= maxBytes.Value)
                {
                    throw new IOException("External POP3 control line exceeded the 250000-byte budget without CRLF.");
                }
            }

            var line = output.ToArray();
            if (line.Length > 0 && line[^1] == (byte)'\r')
            {
                Array.Resize(ref line, line.Length - 1);
            }

            return line;
        }
    }
}
