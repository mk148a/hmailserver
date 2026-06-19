namespace HMailServer.Core.Abstractions;

public sealed record SmtpReverseDnsResult(
    bool Rejected,
    string ClientIPAddress,
    IReadOnlyList<string> HostNames,
    string FailureReason,
    string FailureResponse)
{
    public static SmtpReverseDnsResult Passed { get; } =
        new(
            Rejected: false,
            ClientIPAddress: string.Empty,
            HostNames: Array.Empty<string>(),
            FailureReason: string.Empty,
            FailureResponse: string.Empty);

    public static SmtpReverseDnsResult Reject(
        string clientIPAddress,
        IReadOnlyList<string> hostNames,
        string failureReason,
        string failureResponse) =>
        new(
            Rejected: true,
            clientIPAddress,
            hostNames,
            failureReason,
            failureResponse);
}
