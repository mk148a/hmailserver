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

    private static async Task WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}
