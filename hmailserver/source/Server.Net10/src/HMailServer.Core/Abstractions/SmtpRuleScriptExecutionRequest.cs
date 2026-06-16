namespace HMailServer.Core.Abstractions;

public sealed record SmtpRuleScriptExecutionRequest(
    string FunctionName,
    long RuleId,
    string RuleName,
    int AccountId,
    string MailFrom,
    IReadOnlyList<SmtpResolvedRecipient> Recipients,
    byte[] MessageData);
