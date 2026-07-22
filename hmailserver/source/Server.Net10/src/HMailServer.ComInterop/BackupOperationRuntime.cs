using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public enum BackupStartDispatchResult
{
    Queued,
    AlreadyRunning,
    QueueUnavailable
}

[ComVisible(false)]
public interface IBackupOperationRuntime
{
    BackupStartDispatchResult TryStartBackup(Func<BackupTaskRequest> taskFactory);

    void OnThreadStopped();
}

[ComVisible(false)]
internal sealed class BackupOperationCoordinator : IBackupOperationRuntime
{
    private readonly object _gate = new();
    private readonly Func<BackupTaskRequest, bool> _tryEnqueueBackupTask;
    private bool _isRunning;

    internal BackupOperationCoordinator(Func<BackupTaskRequest, bool> tryEnqueueBackupTask)
    {
        ArgumentNullException.ThrowIfNull(tryEnqueueBackupTask);
        _tryEnqueueBackupTask = tryEnqueueBackupTask;
    }

    internal bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _isRunning;
            }
        }
    }

    public BackupStartDispatchResult TryStartBackup(Func<BackupTaskRequest> taskFactory)
    {
        ArgumentNullException.ThrowIfNull(taskFactory);

        lock (_gate)
        {
            if (_isRunning)
            {
                return BackupStartDispatchResult.AlreadyRunning;
            }

            _isRunning = true;
            try
            {
                if (!_tryEnqueueBackupTask(taskFactory()))
                {
                    _isRunning = false;
                    return BackupStartDispatchResult.QueueUnavailable;
                }
            }
            catch
            {
                _isRunning = false;
                throw;
            }

            return BackupStartDispatchResult.Queued;
        }
    }

    public void OnThreadStopped()
    {
        lock (_gate)
        {
            _isRunning = false;
        }
    }
}

[ComVisible(false)]
public sealed class BackupOperationRuntime : IBackupOperationRuntime
{
    private readonly BackupOperationCoordinator _coordinator;
    private readonly Func<CancellationToken, ValueTask>? _preflightAsync;

    public BackupOperationRuntime(
        IBackupTaskQueue taskQueue,
        Func<CancellationToken, ValueTask<BackupStartPlanEvidence>>? startPlanEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(taskQueue);
        _coordinator = new(taskQueue.TryEnqueue);
        _preflightAsync = startPlanEvidence is null
            ? null
            : cancellationToken => RunPreflightAsync(startPlanEvidence, cancellationToken);
    }

    public BackupStartDispatchResult TryStartBackup(Func<BackupTaskRequest> taskFactory)
    {
        ArgumentNullException.ThrowIfNull(taskFactory);

        return _coordinator.TryStartBackup(
            _preflightAsync is null
                ? taskFactory
                : () => WrapTaskWithPreflight(taskFactory()));
    }

    public void OnThreadStopped() => _coordinator.OnThreadStopped();

    private BackupTaskRequest WrapTaskWithPreflight(BackupTaskRequest task)
    {
        var preflightAsync = _preflightAsync!;
        return new BackupTaskRequest(
            cancellationToken => ExecuteWithPreflightAsync(
                preflightAsync,
                task.ExecuteAsync,
                cancellationToken),
            task.SetStatus,
            task.Failed,
            task.Completed,
            task.ThreadStopped);
    }

    private static async ValueTask RunPreflightAsync(
        Func<CancellationToken, ValueTask<BackupStartPlanEvidence>> getEvidenceAsync,
        CancellationToken cancellationToken)
    {
        var evidence = await getEvidenceAsync(cancellationToken).ConfigureAwait(false);
        var plan = BackupStartPlan.Evaluate(
            evidence.Destination,
            evidence.BackupOptions,
            evidence.BackupMessagesDbOnly,
            evidence.AllMessageFilesInDataDirectory,
            evidence.DestinationExists);

        if (!plan.CanStart)
        {
            throw new InvalidOperationException(plan.FailureReason);
        }
    }

    private static async ValueTask ExecuteWithPreflightAsync(
        Func<CancellationToken, ValueTask> preflightAsync,
        Func<CancellationToken, ValueTask> executeAsync,
        CancellationToken cancellationToken)
    {
        await preflightAsync(cancellationToken).ConfigureAwait(false);
        await executeAsync(cancellationToken).ConfigureAwait(false);
    }
}

[ComVisible(false)]
public static class BackupManagerRuntimeHost
{
    private static IBackupOperationRuntime? _runtime;

    internal static IBackupOperationRuntime? Runtime => Volatile.Read(ref _runtime);

    public static void Configure(IBackupOperationRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Volatile.Write(ref _runtime, runtime);
    }

    internal static void ResetForTests() => Volatile.Write(ref _runtime, null);
}
