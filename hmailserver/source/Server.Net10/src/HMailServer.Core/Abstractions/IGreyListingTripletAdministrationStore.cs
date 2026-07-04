namespace HMailServer.Core.Abstractions;

public interface IGreyListingTripletAdministrationStore
{
    ValueTask ClearAllAsync(CancellationToken cancellationToken);
}
