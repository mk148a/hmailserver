using System.Buffers;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Pop3;

public sealed class TcpExternalFetchSessionFactory : IExternalFetchSessionFactory
{
    private static readonly Encoding CommandEncoding = Encoding.ASCII;

    private readonly ExternalFetchPop3ClientOptions _options;

    public TcpExternalFetchSessionFactory(ExternalFetchPop3ClientOptions? options = null)
    {
        _options = options ?? new ExternalFetchPop3ClientOptions();
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
            await client.ConnectAsync(account.ServerAddress, account.ServerPort, cancellationToken).ConfigureAwait(false);
            Stream stream = client.GetStream();
            if (account.ConnectionSecurity == ExternalFetchConnectionSecurity.Ssl)
            {
                stream = await UpgradeToTlsAsync(stream, account.ServerAddress, cancellationToken).ConfigureAwait(false);
            }

            var session = new TcpExternalFetchSession(client, stream);
            await session.InitializeAsync(account, cancellationToken).ConfigureAwait(false);
            return session;
        }
        catch
        {
            client.Dispose();
            throw;
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
        private Stream _stream;
        private Pop3LineReader _reader;
        private bool _quitSent;

        public TcpExternalFetchSession(TcpClient client, Stream stream)
        {
            _client = client;
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
                var startTlsResponse = await SendCommandReadLineAsync("STLS", cancellationToken).ConfigureAwait(false);
                if (IsOk(startTlsResponse))
                {
                    _stream = await UpgradeToTlsAsync(_stream, account.ServerAddress, cancellationToken).ConfigureAwait(false);
                    _reader = new Pop3LineReader(_stream);
                }
                else if (account.ConnectionSecurity == ExternalFetchConnectionSecurity.StartTlsRequired)
                {
                    throw new InvalidOperationException("External POP3 server rejected required STLS.");
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
                var lineBytes = await _reader.ReadLineBytesAsync(cancellationToken).ConfigureAwait(false);
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
                var line = await _reader.ReadLineBytesAsync(cancellationToken).ConfigureAwait(false);
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

        public ValueTask DeleteMessageAsync(
            ExternalFetchRemoteMessage message,
            CancellationToken cancellationToken) =>
            SendCommandExpectOkAsync(
                "DELE " + message.SequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                cancellationToken);

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!_quitSent)
                {
                    _quitSent = true;
                    await SendCommandExpectOkAsync("QUIT", CancellationToken.None).ConfigureAwait(false);
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
            finally
            {
                await _stream.DisposeAsync().ConfigureAwait(false);
                _client.Dispose();
            }
        }

        private async ValueTask SendCommandExpectOkAsync(
            string command,
            CancellationToken cancellationToken)
        {
            var response = await SendCommandReadLineAsync(command, cancellationToken).ConfigureAwait(false);
            if (!IsOk(response))
            {
                throw new InvalidOperationException("External POP3 command failed: " + SanitizeResponse(response));
            }
        }

        private async ValueTask<string> SendCommandReadLineAsync(
            string command,
            CancellationToken cancellationToken)
        {
            await SendCommandAsync(command, cancellationToken).ConfigureAwait(false);
            return await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask ReadOkLineAsync(CancellationToken cancellationToken)
        {
            var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
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

        public async ValueTask<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            var line = await ReadLineBytesAsync(cancellationToken).ConfigureAwait(false);
            return CommandEncoding.GetString(line);
        }

        public async ValueTask<byte[]> ReadLineBytesAsync(CancellationToken cancellationToken)
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
                    break;
                }

                output.WriteByte(_singleByte[0]);
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
