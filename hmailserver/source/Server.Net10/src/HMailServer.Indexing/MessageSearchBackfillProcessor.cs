using HMailServer.Core.Abstractions;

namespace HMailServer.Indexing;

public sealed class MessageSearchBackfillProcessor
{
    private readonly IMessageSearchBackfillStore _backfillStore;
    private readonly IMessageSearchDocumentSource _documentSource;
    private readonly IMessageSearchIndex _searchIndex;
    private readonly IMessageIndexingAdministrationStore? _administrationStore;

    public MessageSearchBackfillProcessor(
        IMessageSearchBackfillStore backfillStore,
        IMessageSearchDocumentSource documentSource,
        IMessageSearchIndex searchIndex,
        IMessageIndexingAdministrationStore? administrationStore = null)
    {
        _backfillStore = backfillStore;
        _documentSource = documentSource;
        _searchIndex = searchIndex;
        _administrationStore = administrationStore;
    }

    public async ValueTask<int> RunBatchAsync(
        MessageSearchBackfillOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.LeaseOwner);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.BatchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.LeaseDuration.Ticks);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxAttempts);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.RetryDelay.Ticks, 0);

        if (_administrationStore is not null &&
            !await _administrationStore.IsEnabledAsync(cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        var processed = 0;
        await foreach (var identity in _backfillStore.LeaseBatchAsync(
            options.LeaseOwner,
            options.BatchSize,
            options.LeaseDuration,
            options.MaxAttempts,
            cancellationToken).ConfigureAwait(false))
        {
            await ProcessOneAsync(identity, options, cancellationToken).ConfigureAwait(false);
            processed++;
        }

        return processed;
    }

    private async ValueTask ProcessOneAsync(
        MessageIdentity identity,
        MessageSearchBackfillOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = await _documentSource.TryLoadAsync(identity, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                await _backfillStore.MarkFailedAsync(
                    identity,
                    options.LeaseOwner,
                    "Message search document source returned no document.",
                    options.RetryDelay,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            await _searchIndex.UpsertAsync(document, cancellationToken).ConfigureAwait(false);
            await _backfillStore.MarkSucceededAsync(identity, options.LeaseOwner, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await _backfillStore.MarkFailedAsync(
                identity,
                options.LeaseOwner,
                ex.Message,
                options.RetryDelay,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
