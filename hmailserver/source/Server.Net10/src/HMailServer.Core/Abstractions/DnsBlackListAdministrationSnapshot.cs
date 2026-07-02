namespace HMailServer.Core.Abstractions;

public sealed record DnsBlackListAdministrationSnapshot(
    int Id,
    bool Active,
    string DnsHost,
    string RejectMessage,
    string ExpectedResult,
    int Score);
