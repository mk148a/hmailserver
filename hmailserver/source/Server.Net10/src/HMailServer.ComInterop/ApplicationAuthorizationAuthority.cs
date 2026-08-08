using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(false)]
internal sealed class ApplicationAuthorizationAuthority
{
    private readonly SemaphoreSlim _gate = new(initialCount: 1, maxCount: 1);
    private bool _isServerAdministrator;
    private long _generation;

    internal AuthenticationAttempt BeginAuthentication()
    {
        _gate.Wait();
        try
        {
            _isServerAdministrator = false;
            return new AuthenticationAttempt(++_generation);
        }
        finally
        {
            _gate.Release();
        }
    }

    internal bool CompleteAuthentication(AuthenticationAttempt attempt, bool isServerAdministrator)
    {
        _gate.Wait();
        try
        {
            if (attempt.Generation != _generation)
            {
                return false;
            }

            _isServerAdministrator = isServerAdministrator;
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal bool IsServerAdministrator
    {
        get
        {
            _gate.Wait();
            try
            {
                return _isServerAdministrator;
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    internal long CurrentGeneration
    {
        get
        {
            _gate.Wait();
            try
            {
                return _generation;
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    internal bool IsCurrentAdministrator(long generation)
    {
        _gate.Wait();
        try
        {
            return _isServerAdministrator && _generation == generation;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async ValueTask<IDisposable?> AcquireLeaseAsync(
        long generation,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (!_isServerAdministrator || _generation != generation)
        {
            _gate.Release();
            return null;
        }

        return new Lease(_gate);
    }

    internal readonly record struct AuthenticationAttempt(long Generation);

    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                gate.Release();
            }
        }
    }
}
