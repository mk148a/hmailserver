using System.Net;
using HMailServer.Service;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class RestartableListenerParticipantTests
{
    [TestMethod]
    public async Task StartAndStopDelegatesPreservePerRunEndpointEvidence()
    {
        var endpoint = new IPEndPoint(IPAddress.Loopback, 1143);
        var observed = new List<IPEndPoint>();
        var lifecycle = new RestartableListenerLifecycle((cancellationToken, reportStarted) =>
        {
            reportStarted(endpoint);
            return WaitForCancellationAsync(cancellationToken);
        });
        var participant = new RestartableListenerParticipant(lifecycle);

        await participant.StartAsync(CancellationToken.None, observed.Add);
        await participant.StopAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new[] { endpoint }, observed);
    }

    [TestMethod]
    public async Task WaitForStopAsyncObservesTheActiveListenerRun()
    {
        var lifecycle = new RestartableListenerLifecycle((cancellationToken, reportStarted) =>
        {
            reportStarted(new IPEndPoint(IPAddress.Loopback, 1143));
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });
        var participant = new RestartableListenerParticipant(lifecycle);

        await participant.StartAsync(CancellationToken.None, _ => { });
        var stopTask = participant.WaitForStopAsync();
        Assert.IsFalse(stopTask.IsCompleted);

        await participant.StopAsync(CancellationToken.None);
        await stopTask;
    }

    private static async Task WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}
