using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public sealed record BackupRestoreMetadataResult(int RestoredDomains);

[ComVisible(false)]
public static class BackupRestoreMetadataWriter
{
    public static async ValueTask<BackupRestoreMetadataResult> RestoreDomainsAsync(
        IReadOnlyList<DomainAdministrationSnapshot> domains,
        IDomainAdministrationStore store,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rollbackAsync);

        var restored = 0;
        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: async ct =>
            {
                foreach (var domain in domains)
                {
                    await store.InsertDomainAsync(domain, ct).ConfigureAwait(false);
                    restored++;
                }
            },
            commitAsync: _ => default,
            rollbackAsync: rollbackAsync,
            cancellationToken: cancellationToken);

        return new BackupRestoreMetadataResult(restored);
    }
}