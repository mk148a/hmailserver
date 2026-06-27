namespace HMailServer.Core.Abstractions;

public interface ISecurityRangeAdministrationStore
{
    ValueTask<IReadOnlyList<SecurityRangeAdministrationSnapshot>> GetSecurityRangesAsync(
        CancellationToken cancellationToken);
}
