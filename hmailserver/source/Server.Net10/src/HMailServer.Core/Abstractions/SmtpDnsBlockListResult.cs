namespace HMailServer.Core.Abstractions;

public sealed record SmtpDnsBlockListResult(
    bool Listed,
    string ListHost,
    string QueryHost,
    string ResponseAddress,
    string FailureResponse)
{
    public static SmtpDnsBlockListResult NotListed { get; } =
        new(
            Listed: false,
            ListHost: string.Empty,
            QueryHost: string.Empty,
            ResponseAddress: string.Empty,
            FailureResponse: string.Empty);

    public static SmtpDnsBlockListResult Blocked(
        string listHost,
        string queryHost,
        string responseAddress,
        string failureResponse) =>
        new(
            Listed: true,
            listHost,
            queryHost,
            responseAddress,
            failureResponse);
}
