namespace HMailServer.Core.Abstractions;

public interface IDnsBlackListAdministrationStore
{
    ValueTask<IReadOnlyList<DnsBlackListAdministrationSnapshot>> GetDnsBlackListsAsync(
        CancellationToken cancellationToken);

    ValueTask<int> InsertDnsBlackListAsync(
        DnsBlackListAdministrationSnapshot blackList,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("DNS blacklist insertion is not available in this store.");

    ValueTask<bool> UpdateDnsBlackListAsync(
        DnsBlackListAdministrationSnapshot blackList,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("DNS blacklist updates are not available in this store.");

    ValueTask<bool> DeleteDnsBlackListByIdAsync(
        int databaseId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("DNS blacklist deletion is not available in this store.");
}
