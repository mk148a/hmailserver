namespace HMailServer.Core.Abstractions;

public interface IDnsBlackListAdministrationStore
{
    ValueTask<IReadOnlyList<DnsBlackListAdministrationSnapshot>> GetDnsBlackListsAsync(
        CancellationToken cancellationToken);

    ValueTask<int> InsertDnsBlackListAsync(
        DnsBlackListAdministrationSnapshot blackList,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("DNS blacklist insertion is not available in this store.");
}
