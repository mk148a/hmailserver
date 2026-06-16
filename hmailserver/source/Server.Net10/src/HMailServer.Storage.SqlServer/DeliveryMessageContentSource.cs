using HMailServer.Core.Abstractions;

namespace HMailServer.Storage.SqlServer;

public sealed class DeliveryMessageContentSource : IDeliveryMessageContentSource
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
}
