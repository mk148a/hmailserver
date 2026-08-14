using System.Net;

namespace HMailServer.Service;

internal sealed class RestartableListenerLifecycle : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _transitionGate = new(1, 1);
    private readonly Func<CancellationToken, Action<IPEndPoint>, Task> _runAsync;
    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;

    internal RestartableListenerLifecycle(
        Func<CancellationToken, Action<IPEndPoint>, Task> runAsync)
    {
        ArgumentNullException.ThrowIfNull(runAsync);
        _runAsync = runAsync;
    }

    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        await _transitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StartCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource<IPEndPoint> started = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenSource runCancellation;
        Task runTask;

        lock (_gate)
        {
            if (_runTask is not null)
            {
                throw new InvalidOperationException("The listener lifecycle is already running.");
            }

            runCancellation = new CancellationTokenSource();
            runTask = _runAsync(
                runCancellation.Token,
                endpoint => started.TrySetResult(endpoint));
            _runCancellation = runCancellation;
            _runTask = runTask;
        }

        _ = ObserveStartupFailureAsync(runTask, started);
        try
        {
            await started.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await StopCoreAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal async Task StopAsync(CancellationToken cancellationToken)
    {
        await _transitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    private async Task StopCoreAsync()
    {
        CancellationTokenSource? runCancellation;
        Task? runTask;
        lock (_gate)
        {
            runCancellation = _runCancellation;
            runTask = _runTask;
            if (runCancellation is null || runTask is null)
            {
                return;
            }

            _runCancellation = null;
            _runTask = null;
        }

        runCancellation.Cancel();
        try
        {
            await runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            runCancellation.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _transitionGate.Dispose();
    }

    private static async Task ObserveStartupFailureAsync(
        Task runTask,
        TaskCompletionSource<IPEndPoint> started)
    {
        try
        {
            await runTask.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            started.TrySetException(exception);
        }
    }
}
