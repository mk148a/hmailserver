namespace HMailServer.Core.Abstractions;

public sealed record SmtpReceiveResult(
    bool Accepted,
    string? FailureResponse)
{
    public static SmtpReceiveResult Success() => new(Accepted: true, FailureResponse: null);

    public static SmtpReceiveResult Failure(string response) => new(Accepted: false, FailureResponse: response);
}
