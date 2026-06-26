namespace HMailServer.Core.Abstractions;

public interface IDistributionListAdministrationStore
{
    ValueTask<IReadOnlyList<DistributionListAdministrationSnapshot>> GetDistributionListsAsync(
        int domainId,
        CancellationToken cancellationToken);
}
