using System.Threading.Channels;

namespace HMailServer.Protocols;

public sealed class BoundedWorkQueue<T>
{
    private readonly Channel<T> _channel;

    public BoundedWorkQueue(int capacity, BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            AllowSynchronousContinuations = false,
            FullMode = fullMode,
            SingleReader = false,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(T item, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(item, cancellationToken);
    }

    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    public bool TryComplete(Exception? error = null)
    {
        return _channel.Writer.TryComplete(error);
    }
}
