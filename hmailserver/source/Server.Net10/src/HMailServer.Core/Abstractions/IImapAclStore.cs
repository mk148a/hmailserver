namespace HMailServer.Core.Abstractions;

public interface IImapAclStore
{
    ValueTask<ImapAclListResult> GetAclAsync(
        int requesterAccountId,
        string mailboxName,
        CancellationToken cancellationToken);

    ValueTask<ImapAclRightsResult> GetMyRightsAsync(
        int requesterAccountId,
        string mailboxName,
        CancellationToken cancellationToken);

    ValueTask<ImapAclMutationResult> SetAclAsync(
        int requesterAccountId,
        string mailboxName,
        string identifier,
        ImapAclRightsChange rightsChange,
        CancellationToken cancellationToken);

    ValueTask<ImapAclMutationResult> DeleteAclAsync(
        int requesterAccountId,
        string mailboxName,
        string identifier,
        CancellationToken cancellationToken);
}
