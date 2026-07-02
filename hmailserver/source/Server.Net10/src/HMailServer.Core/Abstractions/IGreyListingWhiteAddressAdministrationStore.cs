namespace HMailServer.Core.Abstractions;

public interface IGreyListingWhiteAddressAdministrationStore
{
    ValueTask<IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot>> GetWhiteAddressesAsync(
        CancellationToken cancellationToken);
}
