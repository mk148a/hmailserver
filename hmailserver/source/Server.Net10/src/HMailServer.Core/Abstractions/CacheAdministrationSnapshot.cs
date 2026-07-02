namespace HMailServer.Core.Abstractions;

public sealed record CacheAdministrationSnapshot(
    bool Enabled,
    int DomainCacheTtl,
    int AccountCacheTtl,
    int AliasCacheTtl,
    int DistributionListCacheTtl);
