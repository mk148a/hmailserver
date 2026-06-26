namespace HMailServer.Core.Abstractions;

public sealed record DistributionListAdministrationSnapshot(
    int Id,
    int DomainId,
    string Address,
    bool Active,
    bool RequireSmtpAuth,
    string RequireSenderAddress,
    int Mode);
