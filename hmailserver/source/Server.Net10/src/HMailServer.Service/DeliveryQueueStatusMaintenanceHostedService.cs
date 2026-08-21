using HMailServer.Storage.SqlServer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMailServer.Service;

public sealed class DeliveryQueueStatusMaintenanceHostedService : BackgroundService
{
    private readonly DeliveryQueueStatusMaintenanceOptions _options;
    private readonly SqlServerDeliveryQueueStatusMaintenanceStore _store;
    private readonly ILogger<DeliveryQueueStatusMaintenanceHostedService> _logger;
    private readonly ServerReadinessSignal _serverReadinessSignal;

    public DeliveryQueueStatusMaintenanceHostedService(
        DeliveryQueueStatusMaintenanceOptions options,
        SqlServerDeliveryQueueStatusMaintenanceStore store,
        ILogger<DeliveryQueueStatusMaintenanceHostedService> logger,
        ServerReadinessSignal serverReadinessSignal)
    {
        _options = options;
        _store = store;
        _logger = logger;
        _serverReadinessSignal = serverReadinessSignal;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _serverReadinessSignal
            .WaitForBootstrapAsync(stoppingToken)
            .ConfigureAwait(false);

        if (!_options.Enabled)
        {
            _logger.LogInformation("Delivery queue status retention cleanup is disabled.");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
            return;
        }

        await RunCleanupAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(_options.CleanupInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await RunCleanupAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async ValueTask RunCleanupAsync(CancellationToken cancellationToken)
    {
        var cutoffUtc = DateTime.UtcNow.Subtract(_options.Retention);
        var totalDeleted = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var deleted = await _store.DeleteExpiredAsync(
                    cutoffUtc,
                    _options.BatchSize,
                    cancellationToken).ConfigureAwait(false);
                totalDeleted += deleted;
                if (deleted < _options.BatchSize)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Delivery queue status retention cleanup failed.");
            return;
        }

        if (totalDeleted > 0)
        {
            _logger.LogInformation(
                "Deleted {StatusEventCount} expired delivery queue status events older than {CutoffUtc:u}.",
                totalDeleted,
                cutoffUtc);
        }
    }
}
