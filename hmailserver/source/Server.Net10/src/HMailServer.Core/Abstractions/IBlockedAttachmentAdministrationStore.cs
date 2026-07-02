namespace HMailServer.Core.Abstractions;

public interface IBlockedAttachmentAdministrationStore
{
    ValueTask<IReadOnlyList<BlockedAttachmentAdministrationSnapshot>> GetBlockedAttachmentsAsync(
        CancellationToken cancellationToken);
}
