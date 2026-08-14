using System.Net;
using HMailServer.Service;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class RestartableListenerLifecycleTests
{
    [TestMethod]
    public async Task StartAndStopCanBeRepeated()
    {
        var runs = new List<CancellationToken>();
        var started = new List<TaskCompletionSource<object?>>();
        var lifecycle = new RestartableListenerLifecycle((cancellationToken, reportStarted) =>
        {
            runs.Add(cancellationToken);
            var completion = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            started.Add(completion);
            reportStarted(new IPEndPoint(IPAddress.Loopback, 1143));
            return WaitForCancellationAsync(cancellationToken, completion.Task);
        });

        await lifecycle.StartAsync(CancellationToken.None);
        await lifecycle.StopAsync(CancellationToken.None);
        await lifecycle.StartAsync(CancellationToken.None);
        await lifecycle.StopAsync(CancellationToken.None);

        Assert.AreEqual(2, runs.Count);
        Assert.IsTrue(runs[0].IsCancellationRequested);
        Assert.IsTrue(runs[1].IsCancellationRequested);
    }

    [TestMethod]
    public async Task StartAsync_PropagatesBindFailureAndLeavesLifecycleStopped()
    {
        var expected = new InvalidOperationException("bind failed");
        var lifecycle = new RestartableListenerLifecycle((_, _) =>
            Task.FromException(expected));

        var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => lifecycle.StartAsync(CancellationToken.None));

        Assert.AreSame(expected, actual);
        await lifecycle.StopAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task StartAsync_RejectsConcurrentStart()
    {
        var release = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycle = new RestartableListenerLifecycle((cancellationToken, reportStarted) =>
        {
            reportStarted(new IPEndPoint(IPAddress.Loopback, 1143));
            return WaitForCancellationAsync(cancellationToken, release.Task);
        });

        await lifecycle.StartAsync(CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => lifecycle.StartAsync(CancellationToken.None));

        await lifecycle.StopAsync(CancellationToken.None);
        release.TrySetResult(null);
    }

    private static async Task WaitForCancellationAsync(
        CancellationToken cancellationToken,
        Task completion)
    {
        await Task.WhenAny(completion, Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
    }
}
