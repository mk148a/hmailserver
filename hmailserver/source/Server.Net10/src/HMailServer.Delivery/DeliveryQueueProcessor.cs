using HMailServer.Core.Abstractions;

namespace HMailServer.Delivery;

public sealed class DeliveryQueueProcessor
{
    private readonly IDeliveryQueueLeaseStore _leaseStore;
    private readonly IDeliveryQueueMessageStore _messageStore;
    private readonly IDeliveryTargetResolver _targetResolver;
    private readonly IDeliveryTargetDispatcher _targetDispatcher;
    private readonly IDeliveryQueueRecipientStore _recipientStore;
    private readonly IDeliveryBounceStore _bounceStore;

    public DeliveryQueueProcessor(
        IDeliveryQueueLeaseStore leaseStore,
        IDeliveryQueueMessageStore messageStore,
        IDeliveryTargetResolver targetResolver,
        IDeliveryTargetDispatcher targetDispatcher,
        IDeliveryQueueRecipientStore recipientStore,
        IDeliveryBounceStore bounceStore)
    {
        _leaseStore = leaseStore;
        _messageStore = messageStore;
        _targetResolver = targetResolver;
        _targetDispatcher = targetDispatcher;
        _recipientStore = recipientStore;
        _bounceStore = bounceStore;
    }

    public async ValueTask<int> RunBatchAsync(
        DeliveryQueueProcessorOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.LeaseOwner);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.BatchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.LeaseDuration.Ticks);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.RetryDelay.Ticks, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxRetries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxRetryDelay.Ticks);

        var processed = 0;
        await foreach (var identity in _leaseStore.LeaseReadyMessagesAsync(
            options.LeaseOwner,
            options.BatchSize,
            options.LeaseDuration,
            cancellationToken).ConfigureAwait(false))
        {
            await ProcessOneAsync(identity, options, cancellationToken).ConfigureAwait(false);
            processed++;
        }

        return processed;
    }

    private async ValueTask ProcessOneAsync(
        MessageIdentity identity,
        DeliveryQueueProcessorOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = await _messageStore.TryLoadAsync(identity, options.LeaseOwner, cancellationToken).ConfigureAwait(false);
            if (message is null)
            {
                await _leaseStore.ReleaseAsync(identity.MessageId, options.LeaseOwner, cancellationToken).ConfigureAwait(false);
                return;
            }

            var targetBatches = await _targetResolver.ResolveAsync(message, cancellationToken).ConfigureAwait(false);
            if (targetBatches.Count == 0)
            {
                await _leaseStore.CompleteAsync(identity.MessageId, options.LeaseOwner, cancellationToken).ConfigureAwait(false);
                return;
            }

            foreach (var targetBatch in targetBatches)
            {
                var result = await _targetDispatcher.DispatchAsync(message, targetBatch, cancellationToken).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    await DeleteRecipientsAsync(
                        message,
                        options.LeaseOwner,
                        targetBatch,
                        cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (ShouldBounce(message, result, options))
                {
                    await _bounceStore.SubmitBounceAsync(
                        message,
                        targetBatch.Recipients,
                        result.Error ?? "Delivery failed.",
                        cancellationToken).ConfigureAwait(false);
                    await DeleteRecipientsAsync(
                        message,
                        options.LeaseOwner,
                        targetBatch,
                        cancellationToken).ConfigureAwait(false);
                    continue;
                }

                await _leaseStore.DeferAsync(
                    identity.MessageId,
                    options.LeaseOwner,
                    result.RetryDelay ?? CalculateRetryDelay(message.CurrentRetryCount, options),
                    incrementRetryCount: true,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            await _leaseStore.CompleteAsync(identity.MessageId, options.LeaseOwner, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            await _leaseStore.DeferAsync(
                identity.MessageId,
                options.LeaseOwner,
                options.RetryDelay,
                incrementRetryCount: true,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask DeleteRecipientsAsync(
        DeliveryQueuedMessage message,
        string leaseOwner,
        DeliveryTargetBatch targetBatch,
        CancellationToken cancellationToken)
    {
        var recipientIds = targetBatch.Recipients.Select(static recipient => recipient.RecipientId).ToArray();
        if (recipientIds.Length == 0)
        {
            return;
        }

        await _recipientStore.DeleteRecipientsAsync(
            message.Identity.MessageId,
            leaseOwner,
            recipientIds,
            cancellationToken).ConfigureAwait(false);
    }

    private static bool ShouldBounce(
        DeliveryQueuedMessage message,
        DeliveryTargetDispatchResult result,
        DeliveryQueueProcessorOptions options)
    {
        return result.FailureKind == DeliveryFailureKind.Permanent ||
               message.CurrentRetryCount >= options.MaxRetries;
    }

    private static TimeSpan CalculateRetryDelay(
        int currentRetryCount,
        DeliveryQueueProcessorOptions options)
    {
        var multiplier = Math.Pow(2, Math.Max(0, currentRetryCount));
        var ticks = checked((long)Math.Min(options.MaxRetryDelay.Ticks, options.RetryDelay.Ticks * multiplier));
        return TimeSpan.FromTicks(ticks);
    }
}
