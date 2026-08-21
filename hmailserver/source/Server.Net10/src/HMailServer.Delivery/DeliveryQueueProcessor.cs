using HMailServer.Core.Abstractions;

namespace HMailServer.Delivery;

public sealed class DeliveryQueueProcessor : IDeliveryQueueBatchProcessor
{
    private const int DeliveringMessageState = 1;

    private readonly IDeliveryQueueLeaseStore _leaseStore;
    private readonly IDeliveryQueueMessageStore _messageStore;
    private readonly IDeliveryTargetResolver _targetResolver;
    private readonly IDeliveryTargetDispatcher _targetDispatcher;
    private readonly IDeliveryQueueRecipientStore _recipientStore;
    private readonly IDeliveryBounceStore _bounceStore;
    private readonly IDeliveryEventScriptExecutor? _deliveryEventScriptExecutor;
    private readonly IDeliveryMessageContentStore? _messageContentStore;
    private readonly IDkimSigner? _dkimSigner;
    private readonly IDeliveryQueueStatusObserver _statusObserver;

    public DeliveryQueueProcessor(
        IDeliveryQueueLeaseStore leaseStore,
        IDeliveryQueueMessageStore messageStore,
        IDeliveryTargetResolver targetResolver,
        IDeliveryTargetDispatcher targetDispatcher,
        IDeliveryQueueRecipientStore recipientStore,
        IDeliveryBounceStore bounceStore,
        IDeliveryEventScriptExecutor? deliveryEventScriptExecutor = null,
        IDeliveryMessageContentStore? messageContentStore = null,
        IDeliveryQueueStatusObserver? statusObserver = null,
        IDkimSigner? dkimSigner = null)
    {
        _leaseStore = leaseStore;
        _messageStore = messageStore;
        _targetResolver = targetResolver;
        _targetDispatcher = targetDispatcher;
        _recipientStore = recipientStore;
        _bounceStore = bounceStore;
        _deliveryEventScriptExecutor = deliveryEventScriptExecutor;
        _messageContentStore = messageContentStore;
        _dkimSigner = dkimSigner;
        _statusObserver = statusObserver ?? NullDeliveryQueueStatusObserver.Instance;
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
            await RecordStatusAsync(
                DeliveryQueueStatusEventKind.MessageLeased,
                identity,
                options,
                cancellationToken).ConfigureAwait(false);

            var message = await _messageStore.TryLoadAsync(identity, options.LeaseOwner, cancellationToken).ConfigureAwait(false);
            if (message is null)
            {
                await _leaseStore.ReleaseAsync(identity.MessageId, options.LeaseOwner, cancellationToken).ConfigureAwait(false);
                await RecordStatusAsync(
                    DeliveryQueueStatusEventKind.MessageLoadMissing,
                    identity,
                    options,
                    cancellationToken,
                    description: "The leased delivery queue message could not be loaded.").ConfigureAwait(false);
                await RecordStatusAsync(
                    DeliveryQueueStatusEventKind.MessageReleased,
                    identity,
                    options,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            var deliveryStart = await RunMessageDeliveryEventAsync(
                "OnDeliveryStart",
                message,
                options.LeaseOwner,
                cancellationToken).ConfigureAwait(false);
            if (!deliveryStart.Succeeded)
            {
                await DeferAfterDeliveryEventFailureAsync(
                    identity,
                    options,
                    "OnDeliveryStart",
                    deliveryStart.Error,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (deliveryStart.DropMessage)
            {
                await CompleteMessageAsync(
                    identity,
                    options.LeaseOwner,
                    deliveryStart.Message,
                    cancellationToken).ConfigureAwait(false);
                await RecordStatusAsync(
                    DeliveryQueueStatusEventKind.MessageDroppedByEvent,
                    identity,
                    options,
                    cancellationToken,
                    message: deliveryStart.Message,
                    description: "OnDeliveryStart requested message drop.").ConfigureAwait(false);
                await RecordStatusAsync(
                    DeliveryQueueStatusEventKind.MessageCompleted,
                    identity,
                    options,
                    cancellationToken,
                    message: deliveryStart.Message).ConfigureAwait(false);
                return;
            }

            message = deliveryStart.Message;

            var deliverMessage = await RunMessageDeliveryEventAsync(
                "OnDeliverMessage",
                message,
                options.LeaseOwner,
                cancellationToken).ConfigureAwait(false);
            if (!deliverMessage.Succeeded)
            {
                await DeferAfterDeliveryEventFailureAsync(
                    identity,
                    options,
                    "OnDeliverMessage",
                    deliverMessage.Error,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (deliverMessage.DropMessage)
            {
                await CompleteMessageAsync(
                    identity,
                    options.LeaseOwner,
                    deliverMessage.Message,
                    cancellationToken).ConfigureAwait(false);
                await RecordStatusAsync(
                    DeliveryQueueStatusEventKind.MessageDroppedByEvent,
                    identity,
                    options,
                    cancellationToken,
                    message: deliverMessage.Message,
                    description: "OnDeliverMessage requested message drop.").ConfigureAwait(false);
                await RecordStatusAsync(
                    DeliveryQueueStatusEventKind.MessageCompleted,
                    identity,
                    options,
                    cancellationToken,
                    message: deliverMessage.Message).ConfigureAwait(false);
                return;
            }

            message = deliverMessage.Message;

            if (message.CurrentRetryCount == 0 && _dkimSigner is not null && _messageContentStore is not null)
            {
                long? signedMessageSize = null;
                try
                {
                    var messageData = await _messageContentStore
                        .TryLoadAsync(message, cancellationToken)
                        .ConfigureAwait(false);
                    if (messageData is not null)
                    {
                        var signedMessageData = await _dkimSigner
                            .SignAsync(message, messageData, cancellationToken)
                            .ConfigureAwait(false);
                        if (signedMessageData is not null
                            && !signedMessageData.AsSpan().SequenceEqual(messageData)
                            && await _messageContentStore.TrySaveAsync(message, signedMessageData, cancellationToken).ConfigureAwait(false))
                        {
                            signedMessageSize = signedMessageData.LongLength;
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    // DKIM is opportunistic for delivery; retain the original message on failure.
                }

                if (signedMessageSize is long persistedSize)
                {
                    var updated = await _messageStore
                        .TryUpdateSizeAsync(message, persistedSize, options.LeaseOwner, cancellationToken)
                        .ConfigureAwait(false);
                    if (!updated)
                    {
                        throw new InvalidOperationException("The signed delivery message size could not be persisted under the current lease.");
                    }

                    message = message with { Size = persistedSize };
                }
            }

            var targetBatches = await _targetResolver.ResolveAsync(message, cancellationToken).ConfigureAwait(false);
            if (targetBatches.Count == 0)
            {
                await CompleteMessageAsync(
                    identity,
                    options.LeaseOwner,
                    message,
                    cancellationToken).ConfigureAwait(false);
                await RecordStatusAsync(
                    DeliveryQueueStatusEventKind.NoDeliveryTargets,
                    identity,
                    options,
                    cancellationToken,
                    message: message,
                    description: "No local, route, or remote delivery targets were resolved.").ConfigureAwait(false);
                await RecordStatusAsync(
                    DeliveryQueueStatusEventKind.MessageCompleted,
                    identity,
                    options,
                    cancellationToken,
                    message: message).ConfigureAwait(false);
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
                    await RecordStatusAsync(
                        DeliveryQueueStatusEventKind.TargetDeliverySucceeded,
                        identity,
                        options,
                        cancellationToken,
                        targetBatch,
                        message).ConfigureAwait(false);
                    continue;
                }

                if (ShouldBounce(message, result, options))
                {
                    var failureDescription = result.Error ?? "Delivery failed.";
                    message = await RunDeliveryFailedEventsAsync(
                        message,
                        targetBatch.Recipients,
                        failureDescription,
                        options.LeaseOwner,
                        cancellationToken).ConfigureAwait(false);
                    await RecordStatusAsync(
                        DeliveryQueueStatusEventKind.TargetDeliveryFailedPermanently,
                        identity,
                        options,
                        cancellationToken,
                        targetBatch,
                        message,
                        failureKind: result.FailureKind,
                        description: failureDescription).ConfigureAwait(false);
                    var bounceResult = await _bounceStore.SubmitBounceAsync(
                        message,
                        targetBatch.Recipients,
                        failureDescription,
                        cancellationToken).ConfigureAwait(false);
                    await RecordStatusAsync(
                        bounceResult.Submitted
                            ? DeliveryQueueStatusEventKind.BounceSubmitted
                            : DeliveryQueueStatusEventKind.BounceSkipped,
                        identity,
                        options,
                        cancellationToken,
                        targetBatch,
                        message,
                        failureKind: result.FailureKind,
                        description: bounceResult.Reason ?? failureDescription).ConfigureAwait(false);
                    await DeleteRecipientsAsync(
                        message,
                        options.LeaseOwner,
                        targetBatch,
                        cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var retryDelay = result.RetryDelay ?? CalculateRetryDelay(message.CurrentRetryCount, options);
                await _leaseStore.DeferAsync(
                    identity.MessageId,
                    options.LeaseOwner,
                    retryDelay,
                    incrementRetryCount: true,
                    cancellationToken).ConfigureAwait(false);
                await RecordStatusAsync(
                    DeliveryQueueStatusEventKind.TargetDeliveryDeferred,
                    identity,
                    options,
                    cancellationToken,
                    targetBatch,
                    message,
                    retryDelay,
                    result.FailureKind,
                    result.Error).ConfigureAwait(false);
                await RecordStatusAsync(
                    DeliveryQueueStatusEventKind.MessageDeferred,
                    identity,
                    options,
                    cancellationToken,
                    message: message,
                    retryDelay: retryDelay,
                    failureKind: result.FailureKind,
                    description: result.Error).ConfigureAwait(false);
                return;
            }

            await CompleteMessageAsync(
                identity,
                options.LeaseOwner,
                message,
                cancellationToken).ConfigureAwait(false);
            await RecordStatusAsync(
                DeliveryQueueStatusEventKind.MessageCompleted,
                identity,
                options,
                cancellationToken,
                message: message).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _leaseStore.DeferAsync(
                identity.MessageId,
                options.LeaseOwner,
                options.RetryDelay,
                incrementRetryCount: true,
                cancellationToken).ConfigureAwait(false);
            await RecordStatusAsync(
                DeliveryQueueStatusEventKind.ProcessingFailed,
                identity,
                options,
                cancellationToken,
                retryDelay: options.RetryDelay,
                description: exception.Message).ConfigureAwait(false);
            await RecordStatusAsync(
                DeliveryQueueStatusEventKind.MessageDeferred,
                identity,
                options,
                cancellationToken,
                retryDelay: options.RetryDelay,
                description: exception.Message).ConfigureAwait(false);
        }
    }

    private async ValueTask<DeliveryEventOutcome> RunMessageDeliveryEventAsync(
        string eventName,
        DeliveryQueuedMessage message,
        string leaseOwner,
        CancellationToken cancellationToken)
    {
        if (_deliveryEventScriptExecutor is null)
        {
            return DeliveryEventOutcome.Continue(message);
        }

        if (_messageContentStore is null)
        {
            return DeliveryEventOutcome.Failure(message, eventName + " cannot run without a message content store.");
        }

        var messageData = await _messageContentStore.TryLoadAsync(message, cancellationToken).ConfigureAwait(false);
        if (messageData is null)
        {
            return DeliveryEventOutcome.Failure(message, eventName + " could not load message content.");
        }

        var result = _deliveryEventScriptExecutor.Execute(
            new DeliveryEventScriptExecutionRequest(
                eventName,
                message.FromAddress,
                ToResolvedRecipients(message.Recipients),
                messageData,
                MessageId: message.Identity.MessageId,
                MessageUid: message.Identity.Uid,
                MessageState: DeliveringMessageState,
                MessageFlags: message.Flags,
                DeliveryAttempt: message.CurrentRetryCount + 1,
                InternalDateUtc: message.CreatedUtc),
            cancellationToken);
        if (!result.Succeeded)
        {
            return DeliveryEventOutcome.Failure(
                message,
                string.IsNullOrWhiteSpace(result.Error)
                    ? eventName + " failed."
                    : result.Error);
        }

        var resultData = result.MessageData ?? messageData;
        if (!resultData.AsSpan().SequenceEqual(messageData))
        {
            var persisted = await TryPersistMessageContentAndSizeAsync(
                message,
                resultData,
                leaseOwner,
                cancellationToken).ConfigureAwait(false);
            if (!persisted)
            {
                return DeliveryEventOutcome.Failure(message, eventName + " could not save mutated message content.");
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
        string leaseOwner,
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
                    failureDescription,
                    MessageId: message.Identity.MessageId,
                    MessageUid: message.Identity.Uid,
                    MessageState: DeliveringMessageState,
                    MessageFlags: message.Flags,
                    DeliveryAttempt: message.CurrentRetryCount + 1,
                    InternalDateUtc: message.CreatedUtc),
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

            var persisted = await TryPersistMessageContentAndSizeAsync(
                message,
                resultData,
                leaseOwner,
                cancellationToken).ConfigureAwait(false);
            if (!persisted)
            {
                throw new InvalidOperationException("The delivery-failed message mutation could not be persisted under the current lease.");
            }

            messageData = resultData;
            message = message with { Size = resultData.LongLength };
        }

        return message;
    }

    private async ValueTask<bool> TryPersistMessageContentAndSizeAsync(
        DeliveryQueuedMessage message,
        byte[] messageData,
        string leaseOwner,
        CancellationToken cancellationToken)
    {
        if (_messageContentStore is null)
        {
            return false;
        }

        if (!await _messageContentStore.TrySaveAsync(message, messageData, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return await _messageStore
            .TryUpdateSizeAsync(message, messageData.LongLength, leaseOwner, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask DeferAfterDeliveryEventFailureAsync(
        MessageIdentity identity,
        DeliveryQueueProcessorOptions options,
        string eventName,
        string? error,
        CancellationToken cancellationToken)
    {
        var description = string.IsNullOrWhiteSpace(error)
            ? eventName + " failed."
            : eventName + " failed: " + error;
        await _leaseStore.DeferAsync(
            identity.MessageId,
            options.LeaseOwner,
            options.RetryDelay,
            incrementRetryCount: true,
            cancellationToken).ConfigureAwait(false);
        await RecordStatusAsync(
            DeliveryQueueStatusEventKind.DeliveryEventFailed,
            identity,
            options,
            cancellationToken,
            retryDelay: options.RetryDelay,
            description: description).ConfigureAwait(false);
        await RecordStatusAsync(
            DeliveryQueueStatusEventKind.MessageDeferred,
            identity,
            options,
            cancellationToken,
            retryDelay: options.RetryDelay,
            description: description).ConfigureAwait(false);
    }

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

    private async ValueTask CompleteMessageAsync(
        MessageIdentity identity,
        string leaseOwner,
        DeliveryQueuedMessage message,
        CancellationToken cancellationToken)
    {
        var completed = await _leaseStore
            .CompleteAsync(identity.MessageId, leaseOwner, cancellationToken)
            .ConfigureAwait(false);
        if (!completed || _messageContentStore is null)
        {
            return;
        }

        try
        {
            await _messageContentStore
                .TryDeleteAsync(message, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
        }
    }

    private async ValueTask RecordStatusAsync(
        DeliveryQueueStatusEventKind kind,
        MessageIdentity identity,
        DeliveryQueueProcessorOptions options,
        CancellationToken cancellationToken,
        DeliveryTargetBatch? targetBatch = null,
        DeliveryQueuedMessage? message = null,
        TimeSpan? retryDelay = null,
        DeliveryFailureKind? failureKind = null,
        string? description = null)
    {
        try
        {
            await _statusObserver.RecordAsync(
                new DeliveryQueueStatusEvent(
                    kind,
                    identity.MessageId,
                    options.LeaseOwner,
                    targetBatch?.Target.Key,
                    targetBatch?.Target.DomainName,
                    targetBatch?.Target.Kind,
                    targetBatch?.Recipients.Count ?? 0,
                    message?.CurrentRetryCount ?? 0,
                    retryDelay,
                    failureKind,
                    description),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
        }
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
        bool DropMessage,
        string Error)
    {
        public static DeliveryEventOutcome Continue(DeliveryQueuedMessage message) =>
            new(Succeeded: true, message, DropMessage: false, Error: string.Empty);

        public static DeliveryEventOutcome Drop(DeliveryQueuedMessage message) =>
            new(Succeeded: true, message, DropMessage: true, Error: string.Empty);

        public static DeliveryEventOutcome Failure(
            DeliveryQueuedMessage message,
            string error) =>
            new(Succeeded: false, message, DropMessage: false, error);
    }
}
