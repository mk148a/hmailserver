namespace HMailServer.Core.Abstractions;

public sealed record AutoBanLogonFailureResult(
    bool Enabled,
    int FailureCount,
    bool Disconnect,
    bool RangeCreated);
