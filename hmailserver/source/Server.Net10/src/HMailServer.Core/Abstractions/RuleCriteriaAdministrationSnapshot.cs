namespace HMailServer.Core.Abstractions;

public sealed record RuleCriteriaAdministrationSnapshot(
    int Id,
    int RuleId,
    string MatchValue,
    bool UsePredefined,
    int PredefinedField,
    int MatchType,
    string HeaderField);
