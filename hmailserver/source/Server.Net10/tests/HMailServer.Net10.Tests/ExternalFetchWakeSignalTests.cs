using HMailServer.Service;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ExternalFetchWakeSignalTests
{
    [TestMethod]
    public async Task Signal_CoalescesRepeatedNotifications()
    {
        using var signal = new ExternalFetchWakeSignal();

        signal.Signal();
        signal.Signal();

        Assert.IsTrue(await signal.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None));
        Assert.IsFalse(await signal.WaitAsync(TimeSpan.FromMilliseconds(25), CancellationToken.None));
    }

    [TestMethod]
    public async Task WaitAsync_UnblocksWhenSignaled()
    {
        using var signal = new ExternalFetchWakeSignal();

        var wait = signal.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).AsTask();
        signal.Signal();

        Assert.IsTrue(await wait);
    }

    [TestMethod]
    public async Task WaitAsync_HonorsCancellation()
    {
        using var signal = new ExternalFetchWakeSignal();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => signal.WaitAsync(TimeSpan.FromSeconds(1), cancellation.Token).AsTask());
    }

    [TestMethod]
    public async Task WaitAsync_RejectsNonPositiveTimeout()
    {
        using var signal = new ExternalFetchWakeSignal();

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            () => signal.WaitAsync(TimeSpan.Zero, CancellationToken.None).AsTask());
    }

    [TestMethod]
    public void Dispose_ClosesSignal()
    {
        var signal = new ExternalFetchWakeSignal();
        signal.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(signal.Signal);
    }
}
