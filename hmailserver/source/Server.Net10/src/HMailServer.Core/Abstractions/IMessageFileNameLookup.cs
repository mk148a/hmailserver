namespace HMailServer.Core.Abstractions;

public interface IMessageFileNameLookup
{
    ValueTask<string> GetFileNameByMessageIdAsync(
        long messageId,
        CancellationToken cancellationToken);
}
