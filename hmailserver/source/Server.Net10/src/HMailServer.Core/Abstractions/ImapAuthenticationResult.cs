namespace HMailServer.Core.Abstractions;

public sealed record ImapAuthenticationResult(
    bool Succeeded,
    ImapAuthenticatedAccount? Account,
    string FailureMessage,
    bool IsProtocolError = false)
{
    public static ImapAuthenticationResult Success(ImapAuthenticatedAccount account) =>
        new(true, account, string.Empty);

    public static ImapAuthenticationResult Failure(
        string message,
        bool isProtocolError = false) =>
        new(false, null, message, isProtocolError);
}
