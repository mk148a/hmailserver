using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(false)]
internal enum BackupStartDispatchResult
{
    Queued,
    AlreadyRunning,
    QueueUnavailable
}

[ComVisible(false)]
internal interface IBackupOperationRuntime
{
    BackupStartDispatchResult TryStartBackup();

    void OnThreadStopped();
}

[ComVisible(false)]
internal sealed class BackupOperationCoordinator : IBackupOperationRuntime
{
    private readonly object _gate = new();
    private readonly Func<bool> _tryEnqueueBackupTask;
    private bool _isRunning;

    internal BackupOperationCoordinator(Func<bool> tryEnqueueBackupTask)
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

    public BackupStartDispatchResult TryStartBackup()
    {
        lock (_gate)
        {
            if (_isRunning)
            {
                return BackupStartDispatchResult.AlreadyRunning;
            }

            _isRunning = true;
            if (!_tryEnqueueBackupTask())
            {
                _isRunning = false;
                return BackupStartDispatchResult.QueueUnavailable;
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
internal static class BackupManagerRuntimeHost
{
    private static IBackupOperationRuntime? _runtime;

    internal static IBackupOperationRuntime? Runtime => Volatile.Read(ref _runtime);

    internal static void Configure(IBackupOperationRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Volatile.Write(ref _runtime, runtime);
    }

    internal static void ResetForTests() => Volatile.Write(ref _runtime, null);
}
