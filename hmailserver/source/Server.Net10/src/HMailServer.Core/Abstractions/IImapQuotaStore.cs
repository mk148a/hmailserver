namespace HMailServer.Core.Abstractions;

public interface IImapQuotaStore
{
    ValueTask<ImapQuotaResult> GetQuotaAsync(
        int requesterAccountId,
        string quotaRoot,
        CancellationToken cancellationToken);

    ValueTask<ImapQuotaRootResult> GetQuotaRootAsync(
        int requesterAccountId,
        string mailboxName,
        CancellationToken cancellationToken);

    ValueTask<ImapQuotaMutationResult> SetQuotaAsync(
        int requesterAccountId,
        string quotaRoot,
        long limitKilobytes,
        CancellationToken cancellationToken);
}
