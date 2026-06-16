namespace HMailServer.Core.Abstractions;

public interface IImapIdleNotifier
{
    IAsyncEnumerable<ImapIdleEvent> WatchAsync(
        ImapIdleWatchRequest request,
        CancellationToken cancellationToken);
}
