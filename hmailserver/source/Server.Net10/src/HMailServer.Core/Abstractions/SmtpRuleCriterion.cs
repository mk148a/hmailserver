namespace HMailServer.Core.Abstractions;

public sealed record SmtpRuleCriterion(
    long Id,
    bool UsePredefinedField,
    SmtpRuleCriteriaField PredefinedField,
    string HeaderName,
    SmtpRuleMatchType MatchType,
    string MatchValue);
