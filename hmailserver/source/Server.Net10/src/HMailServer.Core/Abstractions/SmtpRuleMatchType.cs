namespace HMailServer.Core.Abstractions;

public enum SmtpRuleMatchType
{
    None = 0,
    Equals = 1,
    Contains = 2,
    LessThan = 3,
    GreaterThan = 4,
    MatchesRegex = 5,
    NotContains = 6,
    NotEquals = 7,
    Wildcard = 8
}
