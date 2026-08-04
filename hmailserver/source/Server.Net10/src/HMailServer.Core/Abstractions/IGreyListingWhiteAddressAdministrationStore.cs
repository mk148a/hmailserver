namespace HMailServer.Core.Abstractions;

public interface IGreyListingWhiteAddressAdministrationStore
{
    ValueTask<IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot>> GetWhiteAddressesAsync(
        CancellationToken cancellationToken);

    ValueTask<long> InsertWhiteAddressAsync(
        GreyListingWhiteAddressAdministrationSnapshot address,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Greylisting white-address insertion is not available in this store.");
}
