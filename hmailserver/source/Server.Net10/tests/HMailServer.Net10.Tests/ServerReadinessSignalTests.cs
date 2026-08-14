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
    public async Task WaitForBootstrapAsync_DoesNotReleaseFinalReadiness()
    {
        var signal = new HMailServer.Service.ServerReadinessSignal();
        var bootstrapWaitTask = signal.WaitForBootstrapAsync(CancellationToken.None);
        var readinessWaitTask = signal.WaitAsync(CancellationToken.None);

        signal.SetBootstrapComplete();

        await bootstrapWaitTask;
        Assert.IsFalse(readinessWaitTask.IsCompleted);

        signal.SetReady();
        await readinessWaitTask;
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

    [TestMethod]
    public async Task BeginReinitialization_ReplacesOnlyTheCurrentReadinessGeneration()
    {
        var signal = new HMailServer.Service.ServerReadinessSignal();
        signal.SetBootstrapComplete();
        signal.SetReady();
        await signal.WaitAsync(CancellationToken.None);

        var generation = signal.BeginReinitialization();
        var newReadiness = signal.WaitAsync(CancellationToken.None);
        var newBootstrap = signal.WaitForBootstrapAsync(CancellationToken.None);

        Assert.IsFalse(newReadiness.IsCompleted);
        Assert.IsFalse(newBootstrap.IsCompleted);

        generation.SetBootstrapComplete();
        await generation.BootstrapCompletion;
        generation.SetReady();
        await newReadiness;
    }
}
