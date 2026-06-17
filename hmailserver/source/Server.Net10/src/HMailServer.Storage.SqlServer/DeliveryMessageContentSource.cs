using HMailServer.Core.Abstractions;

namespace HMailServer.Storage.SqlServer;

public sealed class DeliveryMessageContentSource : IDeliveryMessageContentStore
{
    private readonly MessageFilePathResolver _pathResolver;

    public DeliveryMessageContentSource(MessageFilePathResolver pathResolver)
    {
        _pathResolver = pathResolver;
    }

    public async ValueTask<byte[]?> TryLoadAsync(
        DeliveryQueuedMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var path = _pathResolver.Resolve(
            message.FileName,
            accountId: 0,
            folderId: 0,
            accountAddress: null);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> TrySaveAsync(
        DeliveryQueuedMessage message,
        byte[] messageData,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(messageData);

        var path = _pathResolver.Resolve(
            message.FileName,
            accountId: 0,
            folderId: 0,
            accountAddress: null);
        if (path is null || !File.Exists(path))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(
            directory,
            Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            await File.WriteAllBytesAsync(tempPath, messageData, cancellationToken).ConfigureAwait(false);
            File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            return true;
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
