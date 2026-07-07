namespace HMailServer.Core.Abstractions;

public interface IImportMessageFromFileRuntime
{
    ValueTask<bool> ImportMessageFromFileAsync(
        string fileName,
        int accountId,
        CancellationToken cancellationToken);
}
