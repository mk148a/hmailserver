namespace HMailServer.Core.Abstractions;

public sealed record SmtpGreylistingResult(
    bool Deferred,
    string RecipientAddress,
    string FailureResponse)
{
    public static SmtpGreylistingResult Passed { get; } =
        new(
            Deferred: false,
            RecipientAddress: string.Empty,
            FailureResponse: string.Empty);

    public static SmtpGreylistingResult Defer(
        string recipientAddress,
        string failureResponse) =>
        new(
            Deferred: true,
            recipientAddress,
            failureResponse);
}
