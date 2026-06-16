namespace HMailServer.Core.Abstractions;

public sealed record ImapQuota(
    string RootName,
    long UsedKilobytes,
    long? LimitKilobytes);
