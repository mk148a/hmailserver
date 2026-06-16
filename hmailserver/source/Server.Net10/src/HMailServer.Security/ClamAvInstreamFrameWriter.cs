using System.Buffers.Binary;

namespace HMailServer.Security;

public static class ClamAvInstreamFrameWriter
{
    public static async ValueTask WriteChunkAsync(
        Stream stream,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Span<byte> lengthPrefix = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthPrefix, checked((uint)payload.Length));
        await stream.WriteAsync(lengthPrefix.ToArray(), cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask WriteEndAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        await stream.WriteAsync(new byte[4], cancellationToken).ConfigureAwait(false);
    }
}
