using HMailServer.Core.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMailServer.Service;

public sealed class BackupTaskHostedService : BackgroundService
{
    private readonly IBackupTaskQueue _queue;
    private readonly ILogger<BackupTaskHostedService> _logger;
    private readonly object _activeTaskGate = new();
    private TaskCompletionSource<object?>? _activeTaskCompletion;

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
            var activeTaskCompletion = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_activeTaskGate)
            {
                _activeTaskCompletion = activeTaskCompletion;
            }

            try
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
            finally
            {
                lock (_activeTaskGate)
                {
                    if (ReferenceEquals(_activeTaskCompletion, activeTaskCompletion))
                    {
                        _activeTaskCompletion = null;
                    }
                }

                activeTaskCompletion.TrySetResult(null);
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
            Task? activeTask;
            lock (_activeTaskGate)
            {
                activeTask = _activeTaskCompletion?.Task;
            }

            if (activeTask is not null)
            {
                await activeTask.ConfigureAwait(false);
            }

            _queue.CompleteAndAbortPending();
        }
    }
}
