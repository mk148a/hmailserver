namespace HMailServer.Core.Abstractions;

public interface IDistributionListAdministrationStore
{
    ValueTask<IReadOnlyList<DistributionListAdministrationSnapshot>> GetDistributionListsAsync(
        int domainId,
        CancellationToken cancellationToken);

    ValueTask<int> InsertDistributionListAsync(
        DistributionListAdministrationSnapshot distributionList,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Distribution list insertion is not available in this store.");

    ValueTask<bool> UpdateDistributionListAsync(
        DistributionListAdministrationSnapshot distributionList,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Distribution list updates are not available in this store.");
}
