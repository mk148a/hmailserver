namespace HMailServer.Core.Abstractions;

public interface IImapMessageAppendStore
{
    ValueTask<ImapAppendResult> AppendAsync(
        ImapAppendRequest request,
        CancellationToken cancellationToken);
}
