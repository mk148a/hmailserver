namespace HMailServer.Core.Abstractions;

public sealed record DomainAdministrationSnapshot(
    int Id,
    string Name,
    bool Active);
