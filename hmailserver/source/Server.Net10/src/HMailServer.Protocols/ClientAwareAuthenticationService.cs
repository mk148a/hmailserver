using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols;

public sealed class ClientAwareAuthenticationService : IClientAwareAuthenticationService
{
    private readonly IImapAccountAuthenticator _accountAuthenticator;
    private readonly IAutoBanLogonFailureRecorder? _autoBanLogonFailureRecorder;

    public ClientAwareAuthenticationService(
        IImapAccountAuthenticator accountAuthenticator,
        IAutoBanLogonFailureRecorder? autoBanLogonFailureRecorder = null)
    {
        _accountAuthenticator = accountAuthenticator;
        _autoBanLogonFailureRecorder = autoBanLogonFailureRecorder;
    }

    public async ValueTask<ClientAuthenticationResult> AuthenticateAsync(
        ClientAuthenticationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var authentication = await _accountAuthenticator
            .AuthenticateAsync(
                request.Username,
                request.Password,
                request.AuthorizationId,
                cancellationToken)
            .ConfigureAwait(false);
        if (authentication.Succeeded && authentication.Account is not null)
        {
            return new ClientAuthenticationResult(authentication, Disconnect: false);
        }

        var disconnect = false;
        if (_autoBanLogonFailureRecorder is not null
            && request.ClientAddress is not null
            && !authentication.IsProtocolError)
        {
            try
            {
                var failure = await _autoBanLogonFailureRecorder
                    .RecordFailureAsync(request.ClientAddress, request.Username, cancellationToken)
                    .ConfigureAwait(false);
                disconnect = failure.Disconnect;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                disconnect = false;
            }
        }

        return new ClientAuthenticationResult(authentication, disconnect);
    }
}
