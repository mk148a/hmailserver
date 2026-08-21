using HMailServer.Core.Abstractions;
using HMailServer.Service;
using Microsoft.Extensions.Logging.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupTaskQueueTests
{
    [TestMethod]
    public async Task EnqueuePublishesOneRequestToTheMaintenanceReader()
    {
        using var queue = new BackupTaskQueue();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var request = CreateRequest();

        Assert.IsTrue(queue.TryEnqueue(request));

        await using var reader = queue
            .ReadAllAsync(cancellation.Token)
            .GetAsyncEnumerator(cancellation.Token);

        Assert.IsTrue(await reader.MoveNextAsync());
        Assert.AreSame(request, reader.Current);
    }

    [TestMethod]
    public void DisposeCompletesTheQueueAndRejectsNewRequests()
    {
        var queue = new BackupTaskQueue();
        queue.Dispose();

        Assert.IsFalse(queue.TryEnqueue(CreateRequest()));
    }

    [TestMethod]
    public void DisposeAbortsPendingRequestsAndIsIdempotent()
    {
        var aborted = 0;
        var threadStopped = 0;
        var queue = new BackupTaskQueue();
        Assert.IsTrue(queue.TryEnqueue(CreateRequest(
            () => Interlocked.Increment(ref aborted),
            () => Interlocked.Increment(ref threadStopped))));

        queue.Dispose();
        queue.CompleteAndAbortPending();

        Assert.AreEqual(1, aborted);
        Assert.AreEqual(1, threadStopped);
        Assert.IsFalse(queue.TryEnqueue(CreateRequest()));
    }

    [TestMethod]
    public void CompleteAndAbortPending_ContinuesAfterAbortCallbackFailure()
    {
        var firstThreadStopped = 0;
        var secondAborted = 0;
        var secondThreadStopped = 0;
        using var queue = new BackupTaskQueue();
        Assert.IsTrue(queue.TryEnqueue(CreateRequest(
            () => throw new InvalidOperationException("abort failed"),
            () => Interlocked.Increment(ref firstThreadStopped))));
        Assert.IsTrue(queue.TryEnqueue(CreateRequest(
            () => Interlocked.Increment(ref secondAborted),
            () => Interlocked.Increment(ref secondThreadStopped))));

        queue.CompleteAndAbortPending();

        Assert.AreEqual(1, firstThreadStopped);
        Assert.AreEqual(1, secondAborted);
        Assert.AreEqual(1, secondThreadStopped);
    }

    [TestMethod]
    public async Task HostedServiceShutdownAbortsPendingRequestsAndRejectsPostShutdownEnqueue()
    {
        var aborted = 0;
        var pendingThreadStopped = 0;
        var runningThreadStopped = 0;
        var runningStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var queue = new BackupTaskQueue();
        Assert.IsTrue(queue.TryEnqueue(new BackupTaskRequest(
            async cancellationToken =>
            {
                runningStarted.TrySetResult(null);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            },
            static _ => { },
            static _ => { },
            static () => { },
            () => Interlocked.Increment(ref runningThreadStopped))));
        Assert.IsTrue(queue.TryEnqueue(CreateRequest(
            () => Interlocked.Increment(ref aborted),
            () => Interlocked.Increment(ref pendingThreadStopped))));
        var readiness = new ServerReadinessSignal();
        readiness.SetBootstrapComplete();
        using var service = new BackupTaskHostedService(
            queue,
            NullLogger<BackupTaskHostedService>.Instance,
            readiness);

        await service.StartAsync(CancellationToken.None);
        await runningStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var stopTask = service.StopAsync(stopTimeout.Token);
        Assert.IsFalse(queue.TryEnqueue(CreateRequest()));
        await stopTask;
        queue.Dispose();

        Assert.AreEqual(1, aborted);
        Assert.AreEqual(1, pendingThreadStopped);
        Assert.AreEqual(1, runningThreadStopped);
        Assert.IsFalse(queue.TryEnqueue(CreateRequest()));
    }

    [TestMethod]
    public async Task HostedServiceShutdownWaitsForActiveTaskAfterCancellationTimeout()
    {
        var runningStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRunning = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var queue = new BackupTaskQueue();
        Assert.IsTrue(queue.TryEnqueue(new BackupTaskRequest(
            async _ =>
            {
                runningStarted.TrySetResult(null);
                await releaseRunning.Task;
            },
            static _ => { },
            static _ => { },
            static () => { },
            static () => { })));
        var readiness = new ServerReadinessSignal();
        readiness.SetBootstrapComplete();
        using var service = new BackupTaskHostedService(
            queue,
            NullLogger<BackupTaskHostedService>.Instance,
            readiness);

        await service.StartAsync(CancellationToken.None);
        await runningStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var stopTask = service.StopAsync(stopTimeout.Token);

        await Task.Delay(TimeSpan.FromMilliseconds(150));
        Assert.IsFalse(stopTask.IsCompleted);

        releaseRunning.SetResult(null);
        await stopTask;
    }

    private static BackupTaskRequest CreateRequest(
        Action? abort = null,
        Action? threadStopped = null) => new(
        static _ => ValueTask.CompletedTask,
        static _ => { },
        static _ => { },
        static () => { },
        threadStopped ?? (() => { }),
        abort);
}
