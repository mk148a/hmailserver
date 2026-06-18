namespace HMailServer.Core.Abstractions;

public sealed record SmtpUrlBlockListResult(
    bool Listed,
    string ListHost,
    string MatchedHost,
    string QueryHost,
    string ResponseAddress,
    string FailureResponse)
{
    public static SmtpUrlBlockListResult NotListed { get; } =
        new(
            Listed: false,
            ListHost: string.Empty,
            MatchedHost: string.Empty,
            QueryHost: string.Empty,
            ResponseAddress: string.Empty,
            FailureResponse: string.Empty);

    public static SmtpUrlBlockListResult Blocked(
        string listHost,
        string matchedHost,
        string queryHost,
        string responseAddress,
        string failureResponse) =>
        new(
            Listed: true,
            listHost,
            matchedHost,
            queryHost,
            responseAddress,
            failureResponse);
}
