namespace HMailServer.Core.Abstractions;

public sealed record DeliveryEventScriptExecutionRequest(
    string EventName,
    string MailFrom,
    IReadOnlyList<SmtpResolvedRecipient> Recipients,
    byte[] MessageData,
    DeliveryEventScriptArgumentShape ArgumentShape = DeliveryEventScriptArgumentShape.MessageOnly,
    string RecipientAddress = "",
    string ErrorMessage = "");
