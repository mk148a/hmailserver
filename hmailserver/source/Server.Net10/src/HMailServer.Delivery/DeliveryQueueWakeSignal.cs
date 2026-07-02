using HMailServer.Core.Abstractions;

namespace HMailServer.Delivery;

public sealed class DeliveryQueueWakeSignal : IDeliveryQueueWakeSignal, IDisposable
{
    private readonly SemaphoreSlim _signal = new(initialCount: 0, maxCount: 1);

    public void Signal()
    {
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    public async ValueTask<bool> WaitAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout.Ticks, 0);
        return await _signal.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => _signal.Dispose();
}
