namespace HMailServer.Core.Abstractions;

public interface IExternalFetchWakeSignal
{
    void Signal();

    ValueTask<bool> WaitAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
