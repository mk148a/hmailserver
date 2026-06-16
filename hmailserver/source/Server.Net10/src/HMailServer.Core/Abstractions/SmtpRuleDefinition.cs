namespace HMailServer.Core.Abstractions;

public sealed record SmtpRuleDefinition(
    long Id,
    string Name,
    bool UseAnd,
    int SortOrder,
    IReadOnlyList<SmtpRuleCriterion> Criteria,
    IReadOnlyList<SmtpRuleAction> Actions);
