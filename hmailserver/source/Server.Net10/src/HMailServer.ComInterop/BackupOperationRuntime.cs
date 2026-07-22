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

    public BackupOperationRuntime(IBackupTaskQueue taskQueue)
    {
        ArgumentNullException.ThrowIfNull(taskQueue);
        _coordinator = new(taskQueue.TryEnqueue);
    }

    public BackupStartDispatchResult TryStartBackup(Func<BackupTaskRequest> taskFactory) =>
        _coordinator.TryStartBackup(taskFactory);

    public void OnThreadStopped() => _coordinator.OnThreadStopped();
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
