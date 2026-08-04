namespace HMailServer.Core.Abstractions;

public interface IGreyListingWhiteAddressAdministrationStore
{
    ValueTask<IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot>> GetWhiteAddressesAsync(
        CancellationToken cancellationToken);

    ValueTask<long> InsertWhiteAddressAsync(
        GreyListingWhiteAddressAdministrationSnapshot address,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Greylisting white-address insertion is not available in this store.");

    ValueTask<bool> UpdateWhiteAddressAsync(
        GreyListingWhiteAddressAdministrationSnapshot address,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Greylisting white-address update is not available in this store.");

    ValueTask<bool> DeleteWhiteAddressByIdAsync(
        long databaseId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Greylisting white-address deletion is not available in this store.");
}
