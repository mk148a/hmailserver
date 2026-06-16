namespace HMailServer.Core.Abstractions;

public sealed record ImapAuthenticatedAccount(
    int AccountId,
    string Address);
