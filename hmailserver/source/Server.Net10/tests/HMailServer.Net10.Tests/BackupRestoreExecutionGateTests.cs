using HMailServer.ComInterop;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupRestoreExecutionGateTests
{
    [TestMethod]
    public async Task TryAcquireAsyncSerializesRestoreOwners()
    {
        var gate = new BackupRestoreExecutionGate();
        using var first = await gate.TryAcquireAsync(TimeSpan.Zero, CancellationToken.None);
        Assert.IsNotNull(first);

        using var blocked = await gate.TryAcquireAsync(TimeSpan.Zero, CancellationToken.None);
        Assert.IsNull(blocked);

        first!.Dispose();
        using var second = await gate.TryAcquireAsync(TimeSpan.Zero, CancellationToken.None);
        Assert.IsNotNull(second);
    }

    [TestMethod]
    public async Task TryAcquireAsyncHonorsCancellation()
    {
        var gate = new BackupRestoreExecutionGate();
        using var first = await gate.TryAcquireAsync(TimeSpan.Zero, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => gate.TryAcquireAsync(Timeout.InfiniteTimeSpan, cancellation.Token).AsTask());
    }

    [TestMethod]
    public async Task LeaseDisposeIsIdempotent()
    {
        var gate = new BackupRestoreExecutionGate();
        var lease = await gate.TryAcquireAsync(TimeSpan.Zero, CancellationToken.None);
        Assert.IsNotNull(lease);

        lease!.Dispose();
        lease.Dispose();

        using var next = await gate.TryAcquireAsync(TimeSpan.Zero, CancellationToken.None);
        Assert.IsNotNull(next);
    }
}
