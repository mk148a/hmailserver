namespace HMailServer.Core.Abstractions;

public interface IDnsBlackListAdministrationStore
{
    ValueTask<IReadOnlyList<DnsBlackListAdministrationSnapshot>> GetDnsBlackListsAsync(
        CancellationToken cancellationToken);
}
