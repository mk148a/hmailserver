namespace HMailServer.Core.Abstractions;

public sealed record DomainAliasAdministrationSnapshot(
    int Id,
    int DomainId,
    string AliasName);
