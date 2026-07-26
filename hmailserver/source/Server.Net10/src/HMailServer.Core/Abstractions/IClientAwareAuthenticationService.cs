namespace HMailServer.Core.Abstractions;

public interface IClientAwareAuthenticationService
{
    ValueTask<ClientAuthenticationResult> AuthenticateAsync(
        ClientAuthenticationRequest request,
        CancellationToken cancellationToken);
}
