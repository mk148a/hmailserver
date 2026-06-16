namespace HMailServer.Core.Abstractions;

public interface ISmtpRecipientValidator
{
    ValueTask<SmtpRecipientValidationResult> ValidateAsync(
        SmtpRecipientValidationRequest request,
        CancellationToken cancellationToken);
}
