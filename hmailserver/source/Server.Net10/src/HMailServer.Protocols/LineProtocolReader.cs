using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace HMailServer.Protocols;

public sealed class LineProtocolReader : IAsyncDisposable
{
    private readonly Encoding _encoding;
    private readonly int _maxLineBytes;
    private readonly PipeReader _reader;

    public LineProtocolReader(Stream stream, int maxLineBytes = 8192, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLineBytes);

        _encoding = encoding ?? Encoding.ASCII;
        _maxLineBytes = maxLineBytes;
        _reader = PipeReader.Create(
            stream,
            new StreamPipeReaderOptions(
                bufferSize: 4096,
                minimumReadSize: 512,
                leaveOpen: true));
    }

    public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var buffer = result.Buffer;

            if (TryReadLine(ref buffer, out var line))
            {
                if (line.Length > _maxLineBytes)
                {
                    throw new InvalidDataException($"Protocol line exceeded {_maxLineBytes} bytes.");
                }

                var decodedLine = Decode(line);
                _reader.AdvanceTo(buffer.Start, buffer.Start);
                return decodedLine;
            }

            if (buffer.Length > _maxLineBytes)
            {
                throw new InvalidDataException($"Protocol line exceeded {_maxLineBytes} bytes.");
            }

            if (result.IsCompleted)
            {
                _reader.AdvanceTo(buffer.End);
                return buffer.IsEmpty ? null : throw new InvalidDataException("Protocol line ended without CRLF terminator.");
            }

            _reader.AdvanceTo(buffer.Start, buffer.End);
        }
    }

    public async ValueTask<byte[]> ReadExactAsync(
        int byteCount,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteCount);

        var bytes = new byte[byteCount];
        var written = 0;
        while (written < byteCount)
        {
            var result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var buffer = result.Buffer;
            var needed = byteCount - written;
            var consumedLength = Math.Min(buffer.Length, needed);

            if (consumedLength > 0)
            {
                var slice = buffer.Slice(0, consumedLength);
                slice.CopyTo(bytes.AsSpan(written, (int)consumedLength));
                written += (int)consumedLength;
                _reader.AdvanceTo(slice.End, buffer.End);
            }
            else
            {
                _reader.AdvanceTo(buffer.Start, buffer.End);
            }

            if (result.IsCompleted && written < byteCount)
            {
                throw new EndOfStreamException("Protocol literal ended before the declared byte count.");
            }
        }

        return bytes;
    }

    public ValueTask DisposeAsync()
    {
        return _reader.CompleteAsync();
    }

    private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
    {
        var reader = new SequenceReader<byte>(buffer);
        if (!reader.TryReadTo(out line, (byte)'\n', advancePastDelimiter: true))
        {
            line = default;
            return false;
        }

        if (line.IsEmpty || line.Slice(line.Length - 1, 1).FirstSpan[0] != '\r')
        {
            throw new InvalidDataException("Protocol line ended without CRLF terminator.");
        }

        line = line.Slice(0, line.Length - 1);
        buffer = buffer.Slice(reader.Position);
        return true;
    }

    private string Decode(ReadOnlySequence<byte> line)
    {
        if (line.IsSingleSegment)
        {
            return _encoding.GetString(line.FirstSpan);
        }

        var rented = ArrayPool<byte>.Shared.Rent((int)line.Length);
        try
        {
            line.CopyTo(rented);
            return _encoding.GetString(rented, 0, (int)line.Length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
