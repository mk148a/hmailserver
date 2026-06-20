namespace HMailServer.Core.Abstractions;

public sealed record DeliveryEventScriptExecutionRequest(
    string EventName,
    string MailFrom,
    IReadOnlyList<SmtpResolvedRecipient> Recipients,
    byte[] MessageData,
    DeliveryEventScriptArgumentShape ArgumentShape = DeliveryEventScriptArgumentShape.MessageOnly,
    string RecipientAddress = "",
    string ErrorMessage = "",
    long MessageId = 0,
    long MessageUid = 0,
    int MessageState = 0,
    int DeliveryAttempt = 1,
    DateTimeOffset? InternalDateUtc = null,
    int MessageFlags = 0);
