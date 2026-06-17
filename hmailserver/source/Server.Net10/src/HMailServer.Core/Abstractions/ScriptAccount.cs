namespace HMailServer.Core.Abstractions;

public sealed record ScriptAccount(
    int AccountId,
    string Address,
    bool Active,
    bool IsActiveDirectoryAccount,
    int DomainId,
    int MaxSizeMegabytes,
    string PersonFirstName,
    string PersonLastName,
    int AdminLevel);
