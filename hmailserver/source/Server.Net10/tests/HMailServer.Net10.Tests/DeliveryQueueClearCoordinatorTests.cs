using HMailServer.Core.Abstractions;
using HMailServer.Delivery;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DeliveryQueueClearCoordinatorTests
{
    [TestMethod]
    public async Task Schedule_DrainsFullBatchesUntilPartialBatch()
    {
        var store = new ScriptedAdministrationStore(2, 2, 1);
        var observer = new RecordingObserver();
        var coordinator = new DeliveryQueueClearCoordinator(
            new DeliveryQueueClearOptions(BatchSize: 2),
            store,
            observer,
            CancellationToken.None);

        coordinator.Schedule();

        Assert.AreEqual(5, await observer.Completion.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(3, store.ClearCallCount);
        CollectionAssert.AreEqual(new[] { 2, 2, 2 }, store.BatchSizes);
    }

    [TestMethod]
    public async Task Schedule_CoalescesCallsWhileClearIsRunning()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new ScriptedAdministrationStore(async (_, cancellationToken) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return 0;
        });
        var observer = new RecordingObserver();
        var coordinator = new DeliveryQueueClearCoordinator(
            new DeliveryQueueClearOptions(BatchSize: 10),
            store,
            observer,
            CancellationToken.None);

        coordinator.Schedule();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Schedule();
        coordinator.Schedule();
        release.TrySetResult();

        Assert.AreEqual(0, await observer.Completion.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(1, store.ClearCallCount);
    }

    [TestMethod]
    public async Task Schedule_ReportsStoreFailure()
    {
        var expected = new InvalidOperationException("database unavailable");
        var store = new ScriptedAdministrationStore(
            (_, _) => ValueTask.FromException<int>(expected));
        var observer = new RecordingObserver();
        var coordinator = new DeliveryQueueClearCoordinator(
            DeliveryQueueClearOptions.Default,
            store,
            observer,
            CancellationToken.None);

        coordinator.Schedule();

        Assert.AreSame(
            expected,
            await observer.Failure.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsFalse(observer.Completion.Task.IsCompleted);
    }

    [TestMethod]
    public void Schedule_DoesNothingAfterShutdownCancellation()
    {
        using var shutdown = new CancellationTokenSource();
        shutdown.Cancel();
        var store = new ScriptedAdministrationStore(0);
        var observer = new RecordingObserver();
        var coordinator = new DeliveryQueueClearCoordinator(
            DeliveryQueueClearOptions.Default,
            store,
            observer,
            shutdown.Token);

        coordinator.Schedule();

        Assert.AreEqual(0, store.ClearCallCount);
        Assert.IsFalse(observer.Completion.Task.IsCompleted);
        Assert.IsFalse(observer.Failure.Task.IsCompleted);
    }

    private sealed class ScriptedAdministrationStore : IDeliveryQueueAdministrationStore
    {
        private readonly Queue<int>? _results;
        private readonly Func<int, CancellationToken, ValueTask<int>>? _handler;

        public ScriptedAdministrationStore(params int[] results)
        {
            _results = new Queue<int>(results);
        }

        public ScriptedAdministrationStore(
            Func<int, CancellationToken, ValueTask<int>> handler)
        {
            _handler = handler;
        }

        public int ClearCallCount { get; private set; }

        public int[] BatchSizes { get; private set; } = [];

        public ValueTask<bool> ResetDeliveryTimeAsync(
            long messageId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<bool> RemoveAsync(
            long messageId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<int> ClearBatchAsync(
            int batchSize,
            CancellationToken cancellationToken)
        {
            ClearCallCount++;
            BatchSizes = [.. BatchSizes, batchSize];
            if (_handler is not null)
            {
                return _handler(batchSize, cancellationToken);
            }

            return ValueTask.FromResult(_results!.Dequeue());
        }
    }

    private sealed class RecordingObserver : IDeliveryQueueClearObserver
    {
        public TaskCompletionSource<int> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<Exception> Failure { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Completed(int removedMessages) =>
            Completion.TrySetResult(removedMessages);

        public void Failed(Exception exception) =>
            Failure.TrySetResult(exception);
    }
}
