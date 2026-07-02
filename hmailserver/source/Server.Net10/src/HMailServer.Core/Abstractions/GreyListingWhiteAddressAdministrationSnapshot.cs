namespace HMailServer.Core.Abstractions;

public sealed record GreyListingWhiteAddressAdministrationSnapshot(
    long Id,
    string StoredIpAddress,
    string Description);
