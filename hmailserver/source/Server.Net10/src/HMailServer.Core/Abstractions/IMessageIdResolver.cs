namespace HMailServer.Core.Abstractions;

public interface IMessageIdResolver
{
    ValueTask<long> RetrieveMessageIdAsync(
        string fileName,
        CancellationToken cancellationToken);
}
