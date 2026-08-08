namespace HMailServer.Core.Abstractions;

public interface IImapAccountAuthenticator
{
    ValueTask<ImapAuthenticationResult> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken);

    ValueTask<ImapAuthenticationResult> AuthenticateAsync(
        string username,
        string password,
        string authorizationId,
        CancellationToken cancellationToken) =>
        AuthenticateAsync(username, password, cancellationToken);
}
