using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(false)]
internal static class BackupRestoreTransactionBoundary
{
    internal static async ValueTask ExecuteAsync(
        Func<CancellationToken, ValueTask> mutateAsync,
        Func<CancellationToken, ValueTask> commitAsync,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutateAsync);
        ArgumentNullException.ThrowIfNull(commitAsync);
        ArgumentNullException.ThrowIfNull(rollbackAsync);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await mutateAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await commitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception mutationFailure)
        {
            try
            {
                await rollbackAsync().ConfigureAwait(false);
            }
            catch (Exception rollbackFailure)
            {
                throw new AggregateException(
                    "Restore transaction rollback failed after the mutation failure.",
                    mutationFailure,
                    rollbackFailure);
            }

            throw;
        }
    }
}
