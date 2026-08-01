using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(false)]
internal sealed class BackupRestoreExecutionGate
{
    private readonly SemaphoreSlim _semaphore = new(initialCount: 1, maxCount: 1);

    internal async ValueTask<Lease?> TryAcquireAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        if (!await _semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new Lease(_semaphore);
    }

    internal sealed class Lease : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private int _disposed;

        internal Lease(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _semaphore.Release();
            }
        }
    }
}
