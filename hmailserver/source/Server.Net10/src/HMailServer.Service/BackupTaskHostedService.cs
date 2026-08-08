using HMailServer.Core.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMailServer.Service;

public sealed class BackupTaskHostedService : BackgroundService
{
    private readonly IBackupTaskQueue _queue;
    private readonly ILogger<BackupTaskHostedService> _logger;

    public BackupTaskHostedService(
        IBackupTaskQueue queue,
        ILogger<BackupTaskHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(logger);
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var task in _queue.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            if (stoppingToken.IsCancellationRequested)
            {
                task.AbortPending();
                continue;
            }

            try
            {
                task.SetStatus("Loading backup settings....");
                await task.ExecuteAsync(stoppingToken).ConfigureAwait(false);
                task.Completed();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "The queued hMailServer backup task failed.");
                task.Failed(exception.Message);
            }
            finally
            {
                task.NotifyThreadStopped();
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.StopAccepting();
        try
        {
            await base.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _queue.CompleteAndAbortPending();
        }
    }
}
