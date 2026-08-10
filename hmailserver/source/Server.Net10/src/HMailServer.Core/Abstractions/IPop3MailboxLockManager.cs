namespace HMailServer.Core.Abstractions;

public interface IPop3MailboxLockManager
{
    void Unlock(int accountId);

    ValueTask<IAsyncDisposable?> TryAcquireAsync(
        ImapAuthenticatedAccount account,
        CancellationToken cancellationToken);
}
