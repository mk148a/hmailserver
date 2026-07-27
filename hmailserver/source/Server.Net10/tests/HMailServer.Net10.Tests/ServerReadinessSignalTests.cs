namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ServerReadinessSignalTests
{
    [TestMethod]
    public async Task WaitAsync_CompletesAfterReadinessIsSet()
    {
        var signal = new HMailServer.Service.ServerReadinessSignal();

        var waitTask = signal.WaitAsync(CancellationToken.None);

        Assert.IsFalse(waitTask.IsCompleted);

        signal.SetReady();

        await waitTask;
    }

    [TestMethod]
    public async Task WaitAsync_PropagatesBootstrapFailure()
    {
        var signal = new HMailServer.Service.ServerReadinessSignal();
        var expected = new InvalidOperationException("Full-Text is unavailable.");

        signal.SetFailure(expected);

        var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => signal.WaitAsync(CancellationToken.None));

        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public async Task WaitAsync_ObservesCallerCancellation()
    {
        var signal = new HMailServer.Service.ServerReadinessSignal();
        using var cancellation = new CancellationTokenSource();
        var waitTask = signal.WaitAsync(cancellation.Token);

        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => waitTask);
    }
}
