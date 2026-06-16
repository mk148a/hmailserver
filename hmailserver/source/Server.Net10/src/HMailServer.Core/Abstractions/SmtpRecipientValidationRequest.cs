namespace HMailServer.Core.Abstractions;

public sealed record SmtpRecipientValidationRequest(
    string MailFrom,
    string RecipientAddress,
    bool SenderAuthenticated);
