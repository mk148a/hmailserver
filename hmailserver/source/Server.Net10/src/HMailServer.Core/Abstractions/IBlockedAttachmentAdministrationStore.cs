namespace HMailServer.Core.Abstractions;

public interface IBlockedAttachmentAdministrationStore
{
    ValueTask<IReadOnlyList<BlockedAttachmentAdministrationSnapshot>> GetBlockedAttachmentsAsync(
        CancellationToken cancellationToken);

    ValueTask<int> InsertBlockedAttachmentAsync(
        BlockedAttachmentAdministrationSnapshot attachment,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Blocked attachment inserts are not implemented by this store.");

    ValueTask UpdateBlockedAttachmentAsync(
        BlockedAttachmentAdministrationSnapshot attachment,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Blocked attachment updates are not implemented by this store.");
}
