using System.Buffers;
using System.Globalization;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Delivery;

public sealed class SmtpRemoteDeliveryClient : IRemoteSmtpClient
{
    private static readonly byte[] CrLf = "\r\n"u8.ToArray();
    private static readonly byte[] DotTerminator = ".\r\n"u8.ToArray();

    private readonly IRemoteSmtpTransportFactory _transportFactory;

    public SmtpRemoteDeliveryClient(IRemoteSmtpTransportFactory transportFactory)
    {
        _transportFactory = transportFactory;
    }

    public async ValueTask<RemoteSmtpSendResult> SendAsync(
        RemoteSmtpSendRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RecipientAddresses.Count == 0)
        {
            return RemoteSmtpSendResult.Failure("Remote SMTP send requires at least one recipient.");
        }

        await using var transport = await _transportFactory.ConnectAsync(request.Endpoint, cancellationToken).ConfigureAwait(false);
        if (request.Endpoint.ConnectionSecurity == RemoteSmtpConnectionSecurity.Ssl)
        {
            await transport.UpgradeToTlsAsync(request.Endpoint.Host, cancellationToken).ConfigureAwait(false);
        }

        var reader = new StreamReader(transport.Stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var writer = new StreamWriter(transport.Stream, Encoding.ASCII, bufferSize: 1024, leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };

        var greeting = await ReadReplyAsync(reader, cancellationToken).ConfigureAwait(false);
        if (!greeting.IsPositiveCompletion)
        {
            return FailureFromReply("Remote SMTP greeting failed: ", greeting);
        }

        var ehlo = await SendCommandAsync(writer, reader, "EHLO " + SanitizeCommandAtom(request.HeloHost), cancellationToken).ConfigureAwait(false);
        if (!ehlo.IsPositiveCompletion)
        {
            ehlo = await SendCommandAsync(writer, reader, "HELO " + SanitizeCommandAtom(request.HeloHost), cancellationToken).ConfigureAwait(false);
            if (!ehlo.IsPositiveCompletion)
            {
                return FailureFromReply("Remote SMTP HELO/EHLO failed: ", ehlo);
            }
        }

        if (request.Endpoint.ConnectionSecurity is RemoteSmtpConnectionSecurity.StartTlsOptional or RemoteSmtpConnectionSecurity.StartTlsRequired)
        {
            if (!ehlo.Lines.Any(static line => line.Contains("STARTTLS", StringComparison.OrdinalIgnoreCase)))
            {
                if (request.Endpoint.ConnectionSecurity == RemoteSmtpConnectionSecurity.StartTlsRequired)
                {
                    return RemoteSmtpSendResult.Failure("Remote SMTP server did not advertise STARTTLS.");
                }
            }
            else
            {
                var startTls = await SendCommandAsync(writer, reader, "STARTTLS", cancellationToken).ConfigureAwait(false);
                if (!startTls.IsPositiveCompletion)
                {
                    return FailureFromReply("Remote SMTP STARTTLS failed: ", startTls);
                }

                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                await transport.UpgradeToTlsAsync(request.Endpoint.Host, cancellationToken).ConfigureAwait(false);
                reader = new StreamReader(transport.Stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                writer = new StreamWriter(transport.Stream, Encoding.ASCII, bufferSize: 1024, leaveOpen: true)
                {
                    NewLine = "\r\n",
                    AutoFlush = true
                };
                ehlo = await SendCommandAsync(writer, reader, "EHLO " + SanitizeCommandAtom(request.HeloHost), cancellationToken).ConfigureAwait(false);
                if (!ehlo.IsPositiveCompletion)
                {
                    return FailureFromReply("Remote SMTP EHLO after STARTTLS failed: ", ehlo);
                }
            }
        }

        if (request.Endpoint.RequiresAuthentication)
        {
            var auth = await AuthenticateLoginAsync(request.Endpoint, writer, reader, cancellationToken).ConfigureAwait(false);
            if (!auth.Succeeded)
            {
                return auth;
            }
        }

        var mailFrom = await SendCommandAsync(
            writer,
            reader,
            "MAIL FROM:<" + SanitizeMailbox(request.SenderAddress) + ">",
            cancellationToken).ConfigureAwait(false);
        if (!mailFrom.IsPositiveCompletion)
        {
            return FailureFromReply("Remote SMTP MAIL FROM failed: ", mailFrom);
        }

        foreach (var recipient in request.RecipientAddresses)
        {
            var rcpt = await SendCommandAsync(
                writer,
                reader,
                "RCPT TO:<" + SanitizeMailbox(recipient) + ">",
                cancellationToken).ConfigureAwait(false);
            if (!rcpt.IsPositiveCompletion)
            {
                return FailureFromReply("Remote SMTP RCPT TO failed for " + recipient + ": ", rcpt);
            }
        }

        var data = await SendCommandAsync(writer, reader, "DATA", cancellationToken).ConfigureAwait(false);
        if (data.Code != 354)
        {
            return FailureFromReply("Remote SMTP DATA command failed: ", data);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        await WriteDotStuffedDataAsync(transport.Stream, request.MessageData, cancellationToken).ConfigureAwait(false);
        var accepted = await ReadReplyAsync(reader, cancellationToken).ConfigureAwait(false);
        if (!accepted.IsPositiveCompletion)
        {
            return FailureFromReply("Remote SMTP DATA body was rejected: ", accepted);
        }

        try
        {
            await SendCommandAsync(writer, reader, "QUIT", cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
        }

        return RemoteSmtpSendResult.Success();
    }

    private static async ValueTask<RemoteSmtpSendResult> AuthenticateLoginAsync(
        RemoteSmtpEndpoint endpoint,
        StreamWriter writer,
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var auth = await SendCommandAsync(writer, reader, "AUTH LOGIN", cancellationToken).ConfigureAwait(false);
        if (auth.Code != 334)
        {
            return FailureFromReply("Remote SMTP AUTH LOGIN was rejected: ", auth);
        }

        var username = Convert.ToBase64String(Encoding.UTF8.GetBytes(endpoint.AuthenticationUsername));
        var usernameReply = await SendCommandAsync(writer, reader, username, cancellationToken).ConfigureAwait(false);
        if (usernameReply.Code != 334)
        {
            return FailureFromReply("Remote SMTP AUTH username was rejected: ", usernameReply);
        }

        var password = Convert.ToBase64String(Encoding.UTF8.GetBytes(endpoint.AuthenticationPassword));
        var passwordReply = await SendCommandAsync(writer, reader, password, cancellationToken).ConfigureAwait(false);
        return passwordReply.Code == 235
            ? RemoteSmtpSendResult.Success()
            : FailureFromReply("Remote SMTP AUTH password was rejected: ", passwordReply);
    }

    private static RemoteSmtpSendResult FailureFromReply(
        string prefix,
        SmtpReply reply)
    {
        return RemoteSmtpSendResult.Failure(
            prefix + reply.Format(),
            failureKind: reply.Code >= 500 ? DeliveryFailureKind.Permanent : DeliveryFailureKind.Transient);
    }

    private static async ValueTask<SmtpReply> SendCommandAsync(
        StreamWriter writer,
        StreamReader reader,
        string command,
        CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync(command.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        return await ReadReplyAsync(reader, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<SmtpReply> ReadReplyAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        int? code = null;
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                throw new IOException("Remote SMTP server closed the connection.");
            }

            lines.Add(line);
            if (line.Length < 3 ||
                !int.TryParse(line.AsSpan(0, 3), NumberStyles.None, CultureInfo.InvariantCulture, out var parsedCode))
            {
                throw new IOException("Remote SMTP server returned an invalid reply: " + line);
            }

            code ??= parsedCode;
            if (line.Length == 3 || line[3] != '-')
            {
                return new SmtpReply(code.Value, lines);
            }
        }
    }

    private static async ValueTask WriteDotStuffedDataAsync(
        Stream stream,
        byte[] messageData,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(2);
        try
        {
            var atLineStart = true;
            byte last = 0;
            foreach (var value in messageData)
            {
                if (atLineStart && value == (byte)'.')
                {
                    buffer[0] = (byte)'.';
                    await stream.WriteAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
                }

                buffer[0] = value;
                await stream.WriteAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
                atLineStart = value == (byte)'\n';
                last = value;
            }

            if (messageData.Length == 0 || last != (byte)'\n')
            {
                await stream.WriteAsync(CrLf, cancellationToken).ConfigureAwait(false);
            }

            await stream.WriteAsync(DotTerminator, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string SanitizeCommandAtom(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Trim();

    private static string SanitizeMailbox(string value) =>
        SanitizeCommandAtom(value)
            .Replace("<", string.Empty, StringComparison.Ordinal)
            .Replace(">", string.Empty, StringComparison.Ordinal);

    private sealed record SmtpReply(
        int Code,
        IReadOnlyList<string> Lines)
    {
        public bool IsPositiveCompletion => Code is >= 200 and <= 299;

        public string Format() => string.Join(" | ", Lines);
    }
}

public sealed class TcpRemoteSmtpTransportFactory : IRemoteSmtpTransportFactory
{
    public async ValueTask<IRemoteSmtpTransport> ConnectAsync(
        RemoteSmtpEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
            return new TcpRemoteSmtpTransport(client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}

public sealed class TcpRemoteSmtpTransport : IRemoteSmtpTransport
{
    private readonly TcpClient _client;
    private Stream _stream;

    public TcpRemoteSmtpTransport(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
    }

    public Stream Stream => _stream;

    public async ValueTask UpgradeToTlsAsync(
        string targetHost,
        CancellationToken cancellationToken)
    {
        var sslStream = new SslStream(_stream, leaveInnerStreamOpen: false);
        await sslStream.AuthenticateAsClientAsync(
            new SslClientAuthenticationOptions { TargetHost = targetHost },
            cancellationToken).ConfigureAwait(false);
        _stream = sslStream;
    }

    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync().ConfigureAwait(false);
        _client.Dispose();
    }
}
