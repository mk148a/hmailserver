namespace HMailServer.Core.Abstractions;

public sealed record RuleAdministrationSnapshot(
    int Id,
    int AccountId,
    string Name,
    bool Active,
    bool UseAnd,
    int SortOrder);
