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
    private readonly IDeliveryEventScriptExecutor? _deliveryEventScriptExecutor;
    private readonly IDeliveryMessageContentStore? _messageContentStore;

    public DeliveryQueueProcessor(
        IDeliveryQueueLeaseStore leaseStore,
        IDeliveryQueueMessageStore messageStore,
        IDeliveryTargetResolver targetResolver,
        IDeliveryTargetDispatcher targetDispatcher,
        IDeliveryQueueRecipientStore recipientStore,
        IDeliveryBounceStore bounceStore,
        IDeliveryEventScriptExecutor? deliveryEventScriptExecutor = null,
        IDeliveryMessageContentStore? messageContentStore = null)
    {
        _leaseStore = leaseStore;
        _messageStore = messageStore;
        _targetResolver = targetResolver;
        _targetDispatcher = targetDispatcher;
        _recipientStore = recipientStore;
        _bounceStore = bounceStore;
        _deliveryEventScriptExecutor = deliveryEventScriptExecutor;
        _messageContentStore = messageContentStore;
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

            var deliveryStart = await RunMessageDeliveryEventAsync(
                "OnDeliveryStart",
                message,
                cancellationToken).ConfigureAwait(false);
            if (!deliveryStart.Succeeded)
            {
                await DeferAfterDeliveryEventFailureAsync(
                    identity,
                    options,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (deliveryStart.DropMessage)
            {
                await _leaseStore.CompleteAsync(identity.MessageId, options.LeaseOwner, cancellationToken).ConfigureAwait(false);
                return;
            }

            message = deliveryStart.Message;

            var deliverMessage = await RunMessageDeliveryEventAsync(
                "OnDeliverMessage",
                message,
                cancellationToken).ConfigureAwait(false);
            if (!deliverMessage.Succeeded)
            {
                await DeferAfterDeliveryEventFailureAsync(
                    identity,
                    options,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (deliverMessage.DropMessage)
            {
                await _leaseStore.CompleteAsync(identity.MessageId, options.LeaseOwner, cancellationToken).ConfigureAwait(false);
                return;
            }

            message = deliverMessage.Message;

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
                    message = await RunDeliveryFailedEventsAsync(
                        message,
                        targetBatch.Recipients,
                        result.Error ?? "Delivery failed.",
                        cancellationToken).ConfigureAwait(false);
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

    private async ValueTask<DeliveryEventOutcome> RunMessageDeliveryEventAsync(
        string eventName,
        DeliveryQueuedMessage message,
        CancellationToken cancellationToken)
    {
        if (_deliveryEventScriptExecutor is null)
        {
            return DeliveryEventOutcome.Continue(message);
        }

        if (_messageContentStore is null)
        {
            return DeliveryEventOutcome.Failure(message);
        }

        var messageData = await _messageContentStore.TryLoadAsync(message, cancellationToken).ConfigureAwait(false);
        if (messageData is null)
        {
            return DeliveryEventOutcome.Failure(message);
        }

        var result = _deliveryEventScriptExecutor.Execute(
            new DeliveryEventScriptExecutionRequest(
                eventName,
                message.FromAddress,
                ToResolvedRecipients(message.Recipients),
                messageData),
            cancellationToken);
        if (!result.Succeeded)
        {
            return DeliveryEventOutcome.Failure(message);
        }

        var resultData = result.MessageData ?? messageData;
        if (!resultData.AsSpan().SequenceEqual(messageData))
        {
            var saved = await _messageContentStore.TrySaveAsync(message, resultData, cancellationToken).ConfigureAwait(false);
            if (!saved)
            {
                return DeliveryEventOutcome.Failure(message);
            }
        }

        var updatedMessage = resultData.LongLength == message.Size
            ? message
            : message with { Size = resultData.LongLength };

        return result.DropMessage
            ? DeliveryEventOutcome.Drop(updatedMessage)
            : DeliveryEventOutcome.Continue(updatedMessage);
    }

    private async ValueTask<DeliveryQueuedMessage> RunDeliveryFailedEventsAsync(
        DeliveryQueuedMessage message,
        IReadOnlyList<DeliveryQueueRecipient> failedRecipients,
        string failureDescription,
        CancellationToken cancellationToken)
    {
        if (_deliveryEventScriptExecutor is null ||
            _messageContentStore is null ||
            failedRecipients.Count == 0)
        {
            return message;
        }

        var messageData = await _messageContentStore.TryLoadAsync(message, cancellationToken).ConfigureAwait(false);
        if (messageData is null)
        {
            return message;
        }

        foreach (var failedRecipient in failedRecipients)
        {
            var result = _deliveryEventScriptExecutor.Execute(
                new DeliveryEventScriptExecutionRequest(
                    "OnDeliveryFailed",
                    message.FromAddress,
                    ToResolvedRecipients(message.Recipients),
                    messageData,
                    DeliveryEventScriptArgumentShape.MessageRecipientAndError,
                    failedRecipient.Address,
                    failureDescription),
                cancellationToken);
            if (!result.Succeeded)
            {
                continue;
            }

            var resultData = result.MessageData ?? messageData;
            if (resultData.AsSpan().SequenceEqual(messageData))
            {
                continue;
            }

            var saved = await _messageContentStore.TrySaveAsync(message, resultData, cancellationToken).ConfigureAwait(false);
            if (!saved)
            {
                continue;
            }

            messageData = resultData;
            message = message with { Size = resultData.LongLength };
        }

        return message;
    }

    private async ValueTask DeferAfterDeliveryEventFailureAsync(
        MessageIdentity identity,
        DeliveryQueueProcessorOptions options,
        CancellationToken cancellationToken) =>
        await _leaseStore.DeferAsync(
            identity.MessageId,
            options.LeaseOwner,
            options.RetryDelay,
            incrementRetryCount: true,
            cancellationToken).ConfigureAwait(false);

    private static SmtpResolvedRecipient[] ToResolvedRecipients(
        IReadOnlyList<DeliveryQueueRecipient> recipients)
    {
        var resolvedRecipients = new SmtpResolvedRecipient[recipients.Count];
        for (var index = 0; index < recipients.Count; index++)
        {
            var recipient = recipients[index];
            resolvedRecipients[index] = new SmtpResolvedRecipient(
                recipient.Address,
                recipient.OriginalAddress,
                recipient.LocalAccountId,
                recipient.LocalAccountId > 0);
        }

        return resolvedRecipients;
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

    private sealed record DeliveryEventOutcome(
        bool Succeeded,
        DeliveryQueuedMessage Message,
        bool DropMessage)
    {
        public static DeliveryEventOutcome Continue(DeliveryQueuedMessage message) =>
            new(Succeeded: true, message, DropMessage: false);

        public static DeliveryEventOutcome Drop(DeliveryQueuedMessage message) =>
            new(Succeeded: true, message, DropMessage: true);

        public static DeliveryEventOutcome Failure(DeliveryQueuedMessage message) =>
            new(Succeeded: false, message, DropMessage: false);
    }
}
