namespace HMailServer.Core.Abstractions;

public sealed record ImapQuotaRootResult(
    ImapQuotaCommandStatus Status,
    string MailboxName,
    ImapQuota? Quota)
{
    public static ImapQuotaRootResult Failure(ImapQuotaCommandStatus status) =>
        new(status, string.Empty, null);
}
