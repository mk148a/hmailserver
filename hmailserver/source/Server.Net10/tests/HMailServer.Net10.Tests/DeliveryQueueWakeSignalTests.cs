using HMailServer.Delivery;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DeliveryQueueWakeSignalTests
{
    [TestMethod]
    public async Task Signal_CoalescesRepeatedNotifications()
    {
        using var signal = new DeliveryQueueWakeSignal();

        signal.Signal();
        signal.Signal();

        Assert.IsTrue(await signal.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None));
        Assert.IsFalse(await signal.WaitAsync(TimeSpan.FromMilliseconds(25), CancellationToken.None));
    }

    [TestMethod]
    public async Task WaitAsync_UnblocksWhenSignaled()
    {
        using var signal = new DeliveryQueueWakeSignal();

        var wait = signal.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).AsTask();
        signal.Signal();

        Assert.IsTrue(await wait);
    }

    [TestMethod]
    public async Task WaitAsync_HonorsCancellation()
    {
        using var signal = new DeliveryQueueWakeSignal();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => signal.WaitAsync(TimeSpan.FromSeconds(1), cancellation.Token).AsTask());
    }

    [TestMethod]
    public async Task WaitAsync_RejectsNonPositiveTimeout()
    {
        using var signal = new DeliveryQueueWakeSignal();

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            () => signal.WaitAsync(TimeSpan.Zero, CancellationToken.None).AsTask());
    }
}
