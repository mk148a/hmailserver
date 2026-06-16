namespace HMailServer.Core.Abstractions;

public interface IImapMessageCopyStore
{
    IAsyncEnumerable<ImapCopiedMessage> CopyAsync(
        ImapCopyRequest request,
        CancellationToken cancellationToken);
}
