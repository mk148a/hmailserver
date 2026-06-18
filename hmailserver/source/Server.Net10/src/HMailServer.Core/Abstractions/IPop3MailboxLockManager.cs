namespace HMailServer.Core.Abstractions;

public interface IPop3MailboxLockManager
{
    ValueTask<IAsyncDisposable?> TryAcquireAsync(
        ImapAuthenticatedAccount account,
        CancellationToken cancellationToken);
}
