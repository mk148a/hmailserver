namespace HMailServer.Core.Abstractions;

public sealed record SmtpSenderDomainMxResult(
    bool Rejected,
    string SenderDomain,
    string FailureReason,
    string FailureResponse)
{
    public static SmtpSenderDomainMxResult Passed { get; } =
        new(
            Rejected: false,
            SenderDomain: string.Empty,
            FailureReason: string.Empty,
            FailureResponse: string.Empty);

    public static SmtpSenderDomainMxResult Reject(
        string senderDomain,
        string failureReason,
        string failureResponse) =>
        new(
            Rejected: true,
            senderDomain,
            failureReason,
            failureResponse);
}
