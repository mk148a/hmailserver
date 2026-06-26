namespace HMailServer.Core.Abstractions;

public sealed record AliasAdministrationSnapshot(
    int Id,
    int DomainId,
    string Name,
    string Value,
    bool Active);
