namespace HMailServer.Core.Abstractions;

public sealed record ImapQuotaResult(
    ImapQuotaCommandStatus Status,
    ImapQuota? Quota)
{
    public static ImapQuotaResult Failure(ImapQuotaCommandStatus status) =>
        new(status, null);
}
