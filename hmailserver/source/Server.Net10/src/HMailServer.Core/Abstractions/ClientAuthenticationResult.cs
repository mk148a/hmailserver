namespace HMailServer.Core.Abstractions;

public sealed record ClientAuthenticationResult(
    ImapAuthenticationResult Authentication,
    bool Disconnect);
