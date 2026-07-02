namespace HMailServer.Core.Abstractions;

public interface IDeliveryQueueWakeSignal
{
    void Signal();

    ValueTask<bool> WaitAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
