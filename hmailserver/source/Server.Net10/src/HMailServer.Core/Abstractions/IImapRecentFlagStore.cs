namespace HMailServer.Core.Abstractions;

public interface IImapRecentFlagStore
{
    ValueTask<IReadOnlyList<long>> CaptureRecentUidsAsync(
        int accountId,
        int folderId,
        bool clearRecentFlags,
        CancellationToken cancellationToken);
}
