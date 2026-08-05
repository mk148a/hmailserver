namespace HMailServer.Core.Abstractions;

public interface IDistributionListRecipientAdministrationStore
{
    ValueTask<IReadOnlyList<DistributionListRecipientAdministrationSnapshot>> GetRecipientsAsync(
        int distributionListId,
        CancellationToken cancellationToken);

    ValueTask<int> InsertDistributionListRecipientAsync(
        DistributionListRecipientAdministrationSnapshot snapshot,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Distribution-list recipient insertion is not available in this store.");
}
