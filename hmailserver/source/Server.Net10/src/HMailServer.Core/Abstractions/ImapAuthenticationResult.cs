namespace HMailServer.Core.Abstractions;

public sealed record ImapAuthenticationResult(
    bool Succeeded,
    ImapAuthenticatedAccount? Account,
    string FailureMessage)
{
    public static ImapAuthenticationResult Success(ImapAuthenticatedAccount account) =>
        new(true, account, string.Empty);

    public static ImapAuthenticationResult Failure(string message) =>
        new(false, null, message);
}
