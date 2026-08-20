using HMailServer.Core.Abstractions;

namespace HMailServer.Delivery;

public sealed class DeliveryQueueWorker
{
    private readonly DeliveryQueueWorkerOptions _workerOptions;
    private readonly DeliveryQueueProcessorOptions _processorOptions;
    private readonly IDeliveryQueueBatchProcessor _processor;
    private readonly IDeliveryQueueWakeSignal _wakeSignal;
    private readonly DeliveryQueuePauseDrainGate _pauseDrainGate;
    private readonly IDeliveryQueueWorkerObserver _observer;

    public DeliveryQueueWorker(
        DeliveryQueueWorkerOptions workerOptions,
        DeliveryQueueProcessorOptions processorOptions,
        IDeliveryQueueBatchProcessor processor,
        IDeliveryQueueWakeSignal wakeSignal,
        DeliveryQueuePauseDrainGate pauseDrainGate,
        IDeliveryQueueWorkerObserver observer)
    {
        ArgumentNullException.ThrowIfNull(workerOptions);
        ArgumentNullException.ThrowIfNull(processorOptions);
        ArgumentNullException.ThrowIfNull(processor);
        ArgumentNullException.ThrowIfNull(wakeSignal);
        ArgumentNullException.ThrowIfNull(pauseDrainGate);
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(workerOptions.IdleWait.Ticks, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(workerOptions.FailureWait.Ticks, 0);

        _workerOptions = workerOptions;
        _processorOptions = processorOptions;
        _processor = processor;
        _wakeSignal = wakeSignal;
        _pauseDrainGate = pauseDrainGate;
        _observer = observer;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            int processed;
            try
            {
                using (await _pauseDrainGate
                    .EnterWorkerAsync(cancellationToken)
                    .ConfigureAwait(false))
                {
                    processed = await _processor
                        .RunBatchAsync(_processorOptions, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _observer.ProcessingFailed(exception);
                await _wakeSignal
                    .WaitAsync(_workerOptions.FailureWait, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (processed >= _processorOptions.BatchSize)
            {
                continue;
            }

            await _wakeSignal
                .WaitAsync(_workerOptions.IdleWait, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
