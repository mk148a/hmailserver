namespace HMailServer.Core.Abstractions;

public interface IDistributionListRecipientAdministrationStore
{
    ValueTask<IReadOnlyList<DistributionListRecipientAdministrationSnapshot>> GetRecipientsAsync(
        int distributionListId,
        CancellationToken cancellationToken);
}
