namespace HMailServer.Core.Abstractions;

public sealed record WhiteListAddressAdministrationSnapshot(
    long Id,
    string LowerIpAddress,
    string UpperIpAddress,
    string EmailAddress,
    string Description);
