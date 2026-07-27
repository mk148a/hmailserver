namespace HMailServer.Core.Abstractions;

public interface ISecurityRangeAdministrationStore
{
    ValueTask<IReadOnlyList<SecurityRangeAdministrationSnapshot>> GetSecurityRangesAsync(
        CancellationToken cancellationToken);

    ValueTask<int> InsertSecurityRangeAsync(
        SecurityRangeAdministrationSnapshot range,
        CancellationToken cancellationToken);
}
