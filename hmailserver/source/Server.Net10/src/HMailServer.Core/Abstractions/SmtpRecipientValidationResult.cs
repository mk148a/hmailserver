namespace HMailServer.Core.Abstractions;

public sealed record SmtpRecipientValidationResult(
    bool Accepted,
    IReadOnlyList<SmtpResolvedRecipient> Recipients,
    string? FailureResponse)
{
    public static SmtpRecipientValidationResult Accept(params SmtpResolvedRecipient[] recipients) =>
        new(Accepted: true, recipients, FailureResponse: null);

    public static SmtpRecipientValidationResult Reject(string response) =>
        new(Accepted: false, Array.Empty<SmtpResolvedRecipient>(), response);
}
