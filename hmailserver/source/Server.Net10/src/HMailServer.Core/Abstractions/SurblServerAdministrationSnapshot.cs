namespace HMailServer.Core.Abstractions;

public sealed record SurblServerAdministrationSnapshot(
    int Id,
    bool Active,
    string DnsHost,
    string RejectMessage,
    int Score);
