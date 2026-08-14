namespace HMailServer.Service;

public sealed class ServerReadinessSignal
{
    private readonly object _gate = new();
    private ServerReadinessGeneration _generation = new();

    public Task WaitAsync(CancellationToken cancellationToken) =>
        Current.ReadinessCompletion.WaitAsync(cancellationToken);

    public Task WaitForBootstrapAsync(CancellationToken cancellationToken) =>
        Current.BootstrapCompletion.WaitAsync(cancellationToken);

    public void SetBootstrapComplete() => Current.SetBootstrapComplete();

    public void SetReady() => Current.SetReady();

    public void SetFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Current.SetFailure(exception);
    }

    public void SetCanceled(CancellationToken cancellationToken)
    {
        Current.SetCanceled(cancellationToken);
    }

    internal ServerReadinessGeneration BeginReinitialization()
    {
        lock (_gate)
        {
            _generation = new ServerReadinessGeneration();
            return _generation;
        }
    }

    private ServerReadinessGeneration Current
    {
        get
        {
            lock (_gate)
            {
                return _generation;
            }
        }
    }
}

internal sealed class ServerReadinessGeneration
{
    private readonly TaskCompletionSource _bootstrapCompletion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _readinessCompletion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task BootstrapCompletion => _bootstrapCompletion.Task;
    internal Task ReadinessCompletion => _readinessCompletion.Task;

    internal void SetBootstrapComplete() => _bootstrapCompletion.TrySetResult();

    internal void SetReady() => _readinessCompletion.TrySetResult();

    internal void SetFailure(Exception exception)
    {
        _bootstrapCompletion.TrySetException(exception);
        _readinessCompletion.TrySetException(exception);
    }

    internal void SetCanceled(CancellationToken cancellationToken)
    {
        _bootstrapCompletion.TrySetCanceled(cancellationToken);
        _readinessCompletion.TrySetCanceled(cancellationToken);
    }
}
