namespace HMailServer.Service;

public sealed class ServerReadinessSignal
{
    private readonly TaskCompletionSource _completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitAsync(CancellationToken cancellationToken) =>
        _completion.Task.WaitAsync(cancellationToken);

    public void SetReady() => _completion.TrySetResult();

    public void SetFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _completion.TrySetException(exception);
    }

    public void SetCanceled(CancellationToken cancellationToken) =>
        _completion.TrySetCanceled(cancellationToken);
}
