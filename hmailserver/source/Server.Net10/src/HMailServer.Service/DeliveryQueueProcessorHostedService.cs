using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMailServer.Service;

public sealed class DeliveryQueueProcessorHostedService : BackgroundService, IDeliveryQueueWorkerObserver
{
    private readonly DeliveryQueueWorker _worker;
    private readonly ILogger<DeliveryQueueProcessorHostedService> _logger;

    public DeliveryQueueProcessorHostedService(
        DeliveryQueueWorkerOptions workerOptions,
        DeliveryQueueProcessorOptions processorOptions,
        IDeliveryQueueBatchProcessor processor,
        IDeliveryQueueWakeSignal wakeSignal,
        ILogger<DeliveryQueueProcessorHostedService> logger)
    {
        _worker = new DeliveryQueueWorker(
            workerOptions,
            processorOptions,
            processor,
            wakeSignal,
            this);
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        _worker.RunAsync(stoppingToken);

    public void ProcessingFailed(Exception exception) =>
        _logger.LogWarning(exception, "Delivery queue batch processing failed; the worker will retry.");
}
