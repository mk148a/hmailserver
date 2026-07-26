using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

internal sealed class CapturingClientAwareAuthenticationService : IClientAwareAuthenticationService
{
    private readonly ImapAuthenticationResult _authentication;
    private readonly bool _disconnect;

    public CapturingClientAwareAuthenticationService(
        ImapAuthenticationResult authentication,
        bool disconnect = false)
    {
        _authentication = authentication;
        _disconnect = disconnect;
    }

    public ClientAuthenticationRequest? LastRequest { get; private set; }

    public ValueTask<ClientAuthenticationResult> AuthenticateAsync(
        ClientAuthenticationRequest request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        return ValueTask.FromResult(
            new ClientAuthenticationResult(_authentication, _disconnect));
    }
}
