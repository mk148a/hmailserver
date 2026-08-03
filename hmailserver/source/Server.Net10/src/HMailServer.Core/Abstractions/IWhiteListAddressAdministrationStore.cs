namespace HMailServer.Core.Abstractions;

public interface IWhiteListAddressAdministrationStore
{
    ValueTask<IReadOnlyList<WhiteListAddressAdministrationSnapshot>> GetWhiteListAddressesAsync(
        CancellationToken cancellationToken);

    ValueTask<long> InsertWhiteListAddressAsync(
        WhiteListAddressAdministrationSnapshot address,
        CancellationToken cancellationToken);
}
