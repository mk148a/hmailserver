using System.Globalization;
using System.Net.Sockets;
using System.Text;

namespace HMailServer.Security;

public sealed class SpamAssassinClient
{
    private static readonly byte[] ProcessCommand = "PROCESS SPAMC/1.2\r\n"u8.ToArray();
    private static readonly byte[] HeaderTerminator = "\r\n\r\n"u8.ToArray();
    private static readonly Encoding HeaderEncoding = Encoding.ASCII;
    private static readonly Encoding MessageHeaderEncoding = Encoding.Latin1;

    private readonly SpamAssassinClientOptions _options;

    public SpamAssassinClient(SpamAssassinClientOptions? options = null)
    {
        _options = options ?? new SpamAssassinClientOptions();
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.Host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.Port);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_options.Timeout.Ticks, 0);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.MaxResponseHeaderBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.MaxResponseBytes);
    }

    public async ValueTask<SpamAssassinScanResult> ProcessAsync(
        ReadOnlyMemory<byte> messageData,
        string envelopeFrom,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        var returnPathHeader = CreateReturnPathHeader(envelopeFrom);
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_options.Host, _options.Port, timeout.Token).ConfigureAwait(false);
            await using var stream = client.GetStream();

            await WriteRequestAsync(stream, messageData, returnPathHeader, timeout.Token).ConfigureAwait(false);
            TryShutdownSend(client);

            var header = await ReadResponseHeaderAsync(stream, timeout.Token).ConfigureAwait(false);
            if (!SpamAssassinResponseValidator.TryReadContentLength(header, out var contentLength))
            {
                return SpamAssassinScanResult.Error(
                    messageData.ToArray(),
                    "SpamAssassin response header was invalid.");
            }

            if (contentLength > _options.MaxResponseBytes)
            {
                return SpamAssassinScanResult.Error(
                    messageData.ToArray(),
                    "SpamAssassin response exceeded the configured maximum size.");
            }

            var processedMessage = await ReadExactAsync(stream, contentLength, timeout.Token).ConfigureAwait(false);
            processedMessage = RemoveInjectedReturnPath(processedMessage, returnPathHeader);
            var classification = ClassifyProcessedMessage(processedMessage);

            return classification.IsSpam
                ? SpamAssassinScanResult.Spam(
                    processedMessage,
                    classification.Score,
                    "Tagged as spam by SpamAssassin.")
                : SpamAssassinScanResult.Clean(
                    processedMessage,
                    "Processed by SpamAssassin.",
                    classification.Score);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SpamAssassinScanResult.Error(
                messageData.ToArray(),
                "SpamAssassin scan timed out.");
        }
        catch (SocketException ex)
        {
            return SpamAssassinScanResult.Error(
                messageData.ToArray(),
                "SpamAssassin socket error: " + ex.Message);
        }
        catch (IOException ex)
        {
            return SpamAssassinScanResult.Error(
                messageData.ToArray(),
                "SpamAssassin I/O error: " + ex.Message);
        }
        catch (InvalidDataException ex)
        {
            return SpamAssassinScanResult.Error(
                messageData.ToArray(),
                ex.Message);
        }
    }

    private static byte[] CreateReturnPathHeader(string envelopeFrom)
    {
        var address = envelopeFrom
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        if (address.Length == 0)
        {
            return "Return-Path: <>\r\n"u8.ToArray();
        }

        address = address.Trim('<', '>');
        return HeaderEncoding.GetBytes("Return-Path: <" + address + ">\r\n");
    }

    private static async ValueTask WriteRequestAsync(
        Stream stream,
        ReadOnlyMemory<byte> messageData,
        byte[] returnPathHeader,
        CancellationToken cancellationToken)
    {
        var contentLength = checked(returnPathHeader.LongLength + messageData.Length);
        var contentLengthHeader = HeaderEncoding.GetBytes(
            "Content-length: " + contentLength.ToString(CultureInfo.InvariantCulture) + "\r\n\r\n");

        await stream.WriteAsync(ProcessCommand, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(contentLengthHeader, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(returnPathHeader, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(messageData, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void TryShutdownSend(TcpClient client)
    {
        try
        {
            client.Client.Shutdown(SocketShutdown.Send);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
        }
    }

    private async ValueTask<string> ReadResponseHeaderAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[1];
        var matchedTerminatorBytes = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new InvalidDataException("SpamAssassin closed the connection before sending a complete response header.");
            }

            output.WriteByte(buffer[0]);
            if (output.Length > _options.MaxResponseHeaderBytes)
            {
                throw new InvalidDataException("SpamAssassin response header exceeded the configured maximum size.");
            }

            matchedTerminatorBytes = buffer[0] == HeaderTerminator[matchedTerminatorBytes]
                ? matchedTerminatorBytes + 1
                : buffer[0] == HeaderTerminator[0]
                    ? 1
                    : 0;
            if (matchedTerminatorBytes == HeaderTerminator.Length)
            {
                break;
            }
        }

        var headerBytes = output.ToArray();
        return HeaderEncoding.GetString(headerBytes, 0, headerBytes.Length - HeaderTerminator.Length);
    }

    private static async ValueTask<byte[]> ReadExactAsync(
        Stream stream,
        int length,
        CancellationToken cancellationToken)
    {
        var output = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(output.AsMemory(offset, length - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new InvalidDataException("SpamAssassin closed the connection before sending the declared response body.");
            }

            offset += read;
        }

        return output;
    }

    private static byte[] RemoveInjectedReturnPath(
        byte[] messageData,
        byte[] returnPathHeader)
    {
        if (messageData.AsSpan().StartsWith(returnPathHeader))
        {
            return messageData.AsSpan(returnPathHeader.Length).ToArray();
        }

        return messageData;
    }

    private static SpamAssassinClassification ClassifyProcessedMessage(byte[] messageData)
    {
        var headerEnd = FindHeaderEnd(messageData);
        if (headerEnd < 0)
        {
            return new SpamAssassinClassification(false, 0);
        }

        var header = MessageHeaderEncoding.GetString(messageData, 0, headerEnd);
        foreach (var line in EnumerateUnfoldedHeaderLines(header))
        {
            const string prefix = "X-Spam-Status:";
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = line[prefix.Length..].TrimStart();
            return new SpamAssassinClassification(
                value.StartsWith("YES", StringComparison.OrdinalIgnoreCase),
                ParseSpamAssassinScore(value));
        }

        return new SpamAssassinClassification(false, 0);
    }

    private static int FindHeaderEnd(byte[] messageData)
    {
        for (var i = 0; i <= messageData.Length - 4; i++)
        {
            if (messageData[i] == (byte)'\r' &&
                messageData[i + 1] == (byte)'\n' &&
                messageData[i + 2] == (byte)'\r' &&
                messageData[i + 3] == (byte)'\n')
            {
                return i;
            }
        }

        return -1;
    }

    private static IEnumerable<string> EnumerateUnfoldedHeaderLines(string header)
    {
        using var reader = new StringReader(header);
        string? current = null;
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
            {
                current = current is null
                    ? line.Trim()
                    : current + " " + line.Trim();
                continue;
            }

            if (current is not null)
            {
                yield return current;
            }

            current = line;
        }

        if (current is not null)
        {
            yield return current;
        }
    }

    private static int ParseSpamAssassinScore(string spamStatusValue)
    {
        var start = spamStatusValue.IndexOf("score=", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return 0;
        }

        start += "score=".Length;
        while (start < spamStatusValue.Length && char.IsWhiteSpace(spamStatusValue[start]))
        {
            start++;
        }

        var sign = 1;
        if (start < spamStatusValue.Length && spamStatusValue[start] == '-')
        {
            sign = -1;
            start++;
        }
        else if (start < spamStatusValue.Length && spamStatusValue[start] == '+')
        {
            start++;
        }

        var end = start;
        while (end < spamStatusValue.Length && char.IsDigit(spamStatusValue[end]))
        {
            end++;
        }

        return end == start
            ? 0
            : sign * int.Parse(spamStatusValue[start..end], CultureInfo.InvariantCulture);
    }

    private sealed record SpamAssassinClassification(bool IsSpam, int Score);
}
