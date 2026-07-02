using HMailServer.Core.Abstractions;

namespace HMailServer.Delivery;

public sealed class DeliveryQueueClearCoordinator : IDeliveryQueueClearCoordinator
{
    private readonly DeliveryQueueClearOptions _options;
    private readonly IDeliveryQueueAdministrationStore _store;
    private readonly IDeliveryQueueClearObserver _observer;
    private readonly CancellationToken _shutdownToken;
    private int _running;

    public DeliveryQueueClearCoordinator(
        DeliveryQueueClearOptions options,
        IDeliveryQueueAdministrationStore store,
        IDeliveryQueueClearObserver observer,
        CancellationToken shutdownToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.BatchSize);

        _options = options;
        _store = store;
        _observer = observer;
        _shutdownToken = shutdownToken;
    }

    public void Schedule()
    {
        if (_shutdownToken.IsCancellationRequested ||
            Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        var removedMessages = 0;
        try
        {
            while (!_shutdownToken.IsCancellationRequested)
            {
                var removed = await _store
                    .ClearBatchAsync(_options.BatchSize, _shutdownToken)
                    .ConfigureAwait(false);
                removedMessages += removed;
                if (removed < _options.BatchSize)
                {
                    NotifyCompleted(removedMessages);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (_shutdownToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            NotifyFailed(exception);
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    private void NotifyCompleted(int removedMessages)
    {
        try
        {
            _observer.Completed(removedMessages);
        }
        catch
        {
        }
    }

    private void NotifyFailed(Exception exception)
    {
        try
        {
            _observer.Failed(exception);
        }
        catch
        {
        }
    }
}
