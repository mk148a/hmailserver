using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMailServer.Service;

public sealed class DeliveryQueueProcessorHostedService : BackgroundService, IDeliveryQueueWorkerObserver
{
    private readonly DeliveryQueueWorker _worker;
    private readonly ILogger<DeliveryQueueProcessorHostedService> _logger;
    private readonly ServerReadinessSignal _serverReadinessSignal;

    public DeliveryQueueProcessorHostedService(
        DeliveryQueueWorkerOptions workerOptions,
        DeliveryQueueProcessorOptions processorOptions,
        IDeliveryQueueBatchProcessor processor,
        IDeliveryQueueWakeSignal wakeSignal,
        DeliveryQueuePauseDrainGate pauseDrainGate,
        ILogger<DeliveryQueueProcessorHostedService> logger,
        ServerReadinessSignal serverReadinessSignal)
    {
        _worker = new DeliveryQueueWorker(
            workerOptions,
            processorOptions,
            processor,
            wakeSignal,
            pauseDrainGate,
            this);
        _logger = logger;
        _serverReadinessSignal = serverReadinessSignal;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _serverReadinessSignal
            .WaitForBootstrapAsync(stoppingToken)
            .ConfigureAwait(false);
        await _worker.RunAsync(stoppingToken).ConfigureAwait(false);
    }

    public void ProcessingFailed(Exception exception) =>
        _logger.LogWarning(exception, "Delivery queue batch processing failed; the worker will retry.");
}
