using HMailServer.Core.Abstractions;

namespace HMailServer.Delivery;

public sealed class DeliveryQueueClearCoordinator : IDeliveryQueueClearCoordinator
{
    private readonly DeliveryQueueClearOptions _options;
    private readonly IDeliveryQueueAdministrationStore _store;
    private readonly IDeliveryQueueClearObserver _observer;
    private readonly DeliveryQueuePauseDrainGate _pauseDrainGate;
    private readonly CancellationToken _shutdownToken;
    private int _running;

    public DeliveryQueueClearCoordinator(
        DeliveryQueueClearOptions options,
        IDeliveryQueueAdministrationStore store,
        IDeliveryQueueClearObserver observer,
        DeliveryQueuePauseDrainGate pauseDrainGate,
        CancellationToken shutdownToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(pauseDrainGate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.BatchSize);

        _options = options;
        _store = store;
        _observer = observer;
        _pauseDrainGate = pauseDrainGate;
        _shutdownToken = shutdownToken;
    }

    public void Schedule(Func<bool>? authorizationGuard = null)
    {
        if (_shutdownToken.IsCancellationRequested ||
            Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(() => RunAsync(authorizationGuard));
    }

    private async Task RunAsync(Func<bool>? authorizationGuard)
    {
        var removedMessages = 0;
        try
        {
            using (await _pauseDrainGate
                .PauseAndDrainAsync(_shutdownToken)
                .ConfigureAwait(false))
            {
                var clearStartedUtc = DateTime.UtcNow;
                while (!_shutdownToken.IsCancellationRequested)
                {
                    if (authorizationGuard is not null && !authorizationGuard())
                    {
                        throw new UnauthorizedAccessException(
                            "Server administrator authorization was revoked during queue clear.");
                    }

                    var removed = await _store
                        .ClearBatchAsync(_options.BatchSize, clearStartedUtc, _shutdownToken)
                        .ConfigureAwait(false);
                    removedMessages += removed;
                    if (removed < _options.BatchSize)
                    {
                        NotifyCompleted(removedMessages);
                        return;
                    }
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
