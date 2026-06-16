namespace HMailServer.Core.Abstractions;

public interface IRemoteSmtpEndpointResolver
{
    ValueTask<RemoteSmtpEndpoint> ResolveAsync(
        DeliveryTarget target,
        CancellationToken cancellationToken);
}
