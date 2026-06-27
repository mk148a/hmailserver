namespace HMailServer.Core.Abstractions;

public sealed record AccountAdministrationSnapshot(
    int Id,
    int DomainId,
    string Address,
    bool Active,
    int AdminLevel,
    int MaxSize = 0,
    string PersonFirstName = "",
    string PersonLastName = "");
