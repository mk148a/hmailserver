namespace HMailServer.Core.Abstractions;

public sealed record DistributionListRecipientAdministrationSnapshot(
    int Id,
    int ListId,
    string Address);
