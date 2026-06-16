namespace HMailServer.Core.Abstractions;

public interface IImapSequenceNumberResolver
{
    ValueTask<IReadOnlyDictionary<long, long>> ResolveMailboxSequenceNumbersAsync(
        int accountId,
        int folderId,
        CancellationToken cancellationToken);
}
