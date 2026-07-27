namespace HMailServer.Service;

public sealed class ServerReadinessSignal
{
    private readonly TaskCompletionSource _bootstrapCompletion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _readinessCompletion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitAsync(CancellationToken cancellationToken) =>
        _readinessCompletion.Task.WaitAsync(cancellationToken);

    public Task WaitForBootstrapAsync(CancellationToken cancellationToken) =>
        _bootstrapCompletion.Task.WaitAsync(cancellationToken);

    public void SetBootstrapComplete() => _bootstrapCompletion.TrySetResult();

    public void SetReady() => _readinessCompletion.TrySetResult();

    public void SetFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _bootstrapCompletion.TrySetException(exception);
        _readinessCompletion.TrySetException(exception);
    }

    public void SetCanceled(CancellationToken cancellationToken)
    {
        _bootstrapCompletion.TrySetCanceled(cancellationToken);
        _readinessCompletion.TrySetCanceled(cancellationToken);
    }
}
