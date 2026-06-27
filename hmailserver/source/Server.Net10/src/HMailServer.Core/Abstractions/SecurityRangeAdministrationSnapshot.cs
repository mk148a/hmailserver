namespace HMailServer.Core.Abstractions;

public sealed record SecurityRangeAdministrationSnapshot(
    int Id,
    string Name,
    string LowerIp,
    string UpperIp,
    int Priority,
    int Options,
    bool Expires,
    DateTime ExpiresTime);
