namespace HMailServer.Core.Abstractions;

public interface IMessageFileNameLookup
{
    ValueTask<string> GetFileNameByMessageIdAsync(
        long messageId,
        CancellationToken cancellationToken);

    ValueTask<long?> GetMessageIdByFileNameAsync(
        string fileName,
        CancellationToken cancellationToken);
}
