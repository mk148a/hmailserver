using HMailServer.Service;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ServerStartupCoordinatorTests
{
    [TestMethod]
    public async Task StartAsync_WaitsForBootstrapAndEveryEnabledListener()
    {
        var signal = new ServerReadinessSignal();
        var listenerStarted = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new ServerStartupCoordinator(signal, [listenerStarted.Task]);

        var startTask = coordinator.StartAsync(CancellationToken.None);

        Assert.IsFalse(startTask.IsCompleted);
        Assert.IsFalse(signal.WaitAsync(CancellationToken.None).IsCompleted);

        signal.SetBootstrapComplete();

        Assert.IsFalse(signal.WaitAsync(CancellationToken.None).IsCompleted);

        listenerStarted.SetResult(null);

        await startTask;
        await signal.WaitAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task StartAsync_TreatsDisabledListenersAsReadyWithoutAwaitingAListenerTask()
    {
        var signal = new ServerReadinessSignal();
        var coordinator = new ServerStartupCoordinator(signal, []);

        signal.SetBootstrapComplete();

        await coordinator.StartAsync(CancellationToken.None);
        await signal.WaitAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task StartAsync_PropagatesListenerStartupFailureToReadiness()
    {
        var signal = new ServerReadinessSignal();
        var expected = new InvalidOperationException("IMAP bind failed.");
        var listenerStarted = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new ServerStartupCoordinator(signal, [listenerStarted.Task]);

        signal.SetBootstrapComplete();
        var startTask = coordinator.StartAsync(CancellationToken.None);
        listenerStarted.SetException(expected);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => startTask);
        var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => signal.WaitAsync(CancellationToken.None));

        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public async Task StartAsync_PropagatesCancellationToReadiness()
    {
        var signal = new ServerReadinessSignal();
        var listenerStarted = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new ServerStartupCoordinator(signal, [listenerStarted.Task]);
        using var cancellation = new CancellationTokenSource();

        signal.SetBootstrapComplete();
        var startTask = coordinator.StartAsync(cancellation.Token);

        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => startTask);
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => signal.WaitAsync(CancellationToken.None));
    }
}
