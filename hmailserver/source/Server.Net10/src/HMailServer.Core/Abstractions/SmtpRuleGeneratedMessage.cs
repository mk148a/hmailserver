namespace HMailServer.Core.Abstractions;

public sealed record SmtpRuleGeneratedMessage(
    string MailFrom,
    IReadOnlyList<SmtpResolvedRecipient> Recipients,
    byte[] MessageData);
