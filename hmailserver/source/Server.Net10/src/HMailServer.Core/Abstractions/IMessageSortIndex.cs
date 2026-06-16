namespace HMailServer.Core.Abstractions;

public interface IMessageSortIndex
{
    IAsyncEnumerable<MessageIdentity> SortAsync(
        ImapSortRequest request,
        CancellationToken cancellationToken);
}
