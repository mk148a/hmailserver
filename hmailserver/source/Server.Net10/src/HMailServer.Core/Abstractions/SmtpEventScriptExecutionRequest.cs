namespace HMailServer.Core.Abstractions;

public sealed record SmtpEventScriptExecutionRequest(
    string EventName,
    SmtpEventScriptClient Client,
    string MailFrom,
    IReadOnlyList<SmtpResolvedRecipient> Recipients,
    byte[] MessageData);
