namespace HMailServer.Delivery;

public sealed class DeliveryQueuePauseDrainGate : IDisposable
{
    private readonly SemaphoreSlim _gate = new(initialCount: 1, maxCount: 1);

    public ValueTask<IDisposable> EnterWorkerAsync(CancellationToken cancellationToken) =>
        AcquireAsync(cancellationToken);

    public ValueTask<IDisposable> PauseAndDrainAsync(CancellationToken cancellationToken) =>
        AcquireAsync(cancellationToken);

    private async ValueTask<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease(_gate);
    }

    public void Dispose() => _gate.Dispose();

    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private SemaphoreSlim? _gate = gate;

        public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Release();
    }
}
