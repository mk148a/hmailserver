using System.Net;

namespace HMailServer.Service;

internal sealed class RestartableListenerParticipant
{
    private readonly RestartableListenerLifecycle _lifecycle;

    internal RestartableListenerParticipant(RestartableListenerLifecycle lifecycle)
    {
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
    }

    internal Task StartAsync(
        CancellationToken cancellationToken,
        Action<IPEndPoint> startedEndpoint) =>
        _lifecycle.StartAsync(cancellationToken, startedEndpoint);

    internal Task StopAsync(CancellationToken cancellationToken) =>
        _lifecycle.StopAsync(cancellationToken);
}
