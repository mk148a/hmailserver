namespace HMailServer.Core.Abstractions;

public interface IImapMailboxSubscriptionStore
{
    ValueTask<ImapMailboxSubscriptionResult> SetSubscribedAsync(
        int requesterAccountId,
        string mailboxName,
        bool subscribed,
        CancellationToken cancellationToken);
}

public enum ImapMailboxSubscriptionStatus
{
    Success,
    MailboxNotFound,
    PermissionDenied,
    PublicFolderNotSupported,
    Failed
}

public sealed record ImapMailboxSubscriptionResult(ImapMailboxSubscriptionStatus Status)
{
    public static ImapMailboxSubscriptionResult Success() =>
        new(ImapMailboxSubscriptionStatus.Success);
}
