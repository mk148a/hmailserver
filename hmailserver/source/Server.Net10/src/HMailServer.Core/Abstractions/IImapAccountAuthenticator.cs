namespace HMailServer.Core.Abstractions;

public interface IImapAccountAuthenticator
{
    ValueTask<ImapAuthenticationResult> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken);
}
