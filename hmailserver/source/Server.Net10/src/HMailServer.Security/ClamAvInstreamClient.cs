using System.Buffers;
using System.Net.Sockets;
using System.Text;

namespace HMailServer.Security;

public sealed class ClamAvInstreamClient
{
    private static readonly byte[] InstreamCommand = "nINSTREAM\n"u8.ToArray();
    private static readonly Encoding ResponseEncoding = Encoding.ASCII;

    private readonly ClamAvInstreamClientOptions _options;

    public ClamAvInstreamClient(ClamAvInstreamClientOptions? options = null)
    {
        _options = options ?? new ClamAvInstreamClientOptions();
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.Host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.Port);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_options.Timeout.Ticks, 0);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.ChunkSize);
    }

    public async ValueTask<ClamAvScanResult> ScanAsync(
        ReadOnlyMemory<byte> messageData,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(messageData.ToArray(), writable: false);
        return await ScanAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ClamAvScanResult> ScanAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_options.Host, _options.Port, timeout.Token).ConfigureAwait(false);
            await using var stream = client.GetStream();

            await stream.WriteAsync(InstreamCommand, timeout.Token).ConfigureAwait(false);
            await WriteContentFramesAsync(content, stream, timeout.Token).ConfigureAwait(false);
            await ClamAvInstreamFrameWriter.WriteEndAsync(stream, timeout.Token).ConfigureAwait(false);
            await stream.FlushAsync(timeout.Token).ConfigureAwait(false);

            var response = await ReadResponseLineAsync(stream, timeout.Token).ConfigureAwait(false);
            return ParseResponse(response);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ClamAvScanResult.Error("ClamAV scan timed out.");
        }
        catch (SocketException ex)
        {
            return ClamAvScanResult.Error("ClamAV socket error: " + ex.Message);
        }
        catch (IOException ex)
        {
            return ClamAvScanResult.Error("ClamAV I/O error: " + ex.Message);
        }
    }

    public static ClamAvScanResult ParseResponse(string response)
    {
        var sanitized = response.Trim();
        const string foundSuffix = " FOUND";
        var marker = sanitized.LastIndexOf(": ", StringComparison.Ordinal);
        if (marker >= 0 && sanitized.EndsWith(foundSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var virusName = sanitized[(marker + 2)..^foundSuffix.Length].Trim();
            return ClamAvScanResult.Infected(virusName, sanitized);
        }

        if (sanitized.EndsWith(" OK", StringComparison.OrdinalIgnoreCase) ||
            sanitized.Equals("OK", StringComparison.OrdinalIgnoreCase))
        {
            return ClamAvScanResult.Clean(sanitized);
        }

        return ClamAvScanResult.Error(sanitized);
    }

    private async ValueTask WriteContentFramesAsync(
        Stream content,
        Stream clamAvStream,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(_options.ChunkSize);
        try
        {
            while (true)
            {
                var read = await content.ReadAsync(buffer.AsMemory(0, _options.ChunkSize), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await ClamAvInstreamFrameWriter
                    .WriteChunkAsync(clamAvStream, buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async ValueTask<string> ReadResponseLineAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new IOException("ClamAV closed the connection before sending a response.");
            }

            if (buffer[0] == (byte)'\n')
            {
                break;
            }

            output.WriteByte(buffer[0]);
        }

        return ResponseEncoding.GetString(output.ToArray()).TrimEnd('\r');
    }
}
