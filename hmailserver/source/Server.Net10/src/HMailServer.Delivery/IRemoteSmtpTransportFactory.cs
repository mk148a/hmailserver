using HMailServer.Core.Abstractions;

namespace HMailServer.Delivery;

public interface IRemoteSmtpTransportFactory
{
    ValueTask<IRemoteSmtpTransport> ConnectAsync(
        RemoteSmtpEndpoint endpoint,
        CancellationToken cancellationToken);
}
