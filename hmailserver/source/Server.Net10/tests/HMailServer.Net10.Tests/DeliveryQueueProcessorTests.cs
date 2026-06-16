using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DeliveryQueueProcessorTests
{
    [TestMethod]
    public async Task RunBatchAsync_DispatchesTargetBatchesAndCompletesLease()
    {
        var identity = new MessageIdentity(10, 0, 0, 0);
        var message = CreateMessage(identity);
        var leaseStore = new FakeLeaseStore(identity);
        var messageStore = new FakeMessageStore(message);
        var localBatch = new DeliveryTargetBatch(
            new DeliveryTarget(DeliveryTargetKind.LocalAccount, "local:42", "example.test", LocalAccountId: 42),
            [message.Recipients[0]]);
        var remoteBatch = new DeliveryTargetBatch(
            new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:remote.test", "remote.test"),
            [message.Recipients[1]]);
        var resolver = new FakeTargetResolver(localBatch, remoteBatch);
        var dispatcher = new FakeTargetDispatcher(
            DeliveryTargetDispatchResult.Success(),
            DeliveryTargetDispatchResult.Success());
        var recipientStore = new FakeRecipientStore();
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            messageStore,
            resolver,
            dispatcher,
            recipientStore,
            new FakeBounceStore());

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(2, dispatcher.Dispatched.Count);
        CollectionAssert.AreEqual(new long[] { 1, 2 }, recipientStore.DeletedRecipientIds);
        Assert.AreEqual(10, leaseStore.CompletedMessageId);
        Assert.IsNull(leaseStore.DeferredMessageId);
    }

    [TestMethod]
    public async Task RunBatchAsync_DefersLeaseWhenDispatcherReturnsTransientFailure()
    {
        var identity = new MessageIdentity(11, 0, 0, 0);
        var message = CreateMessage(identity);
        var leaseStore = new FakeLeaseStore(identity);
        var messageStore = new FakeMessageStore(message);
        var resolver = new FakeTargetResolver(
            new DeliveryTargetBatch(
                new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:remote.test", "remote.test"),
                message.Recipients));
        var dispatcher = new FakeTargetDispatcher(
            DeliveryTargetDispatchResult.TransientFailure("Remote temporary failure.", TimeSpan.FromSeconds(30)));
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            messageStore,
            resolver,
            dispatcher,
            new FakeRecipientStore(),
            new FakeBounceStore());

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(11, leaseStore.DeferredMessageId);
        Assert.AreEqual(TimeSpan.FromSeconds(30), leaseStore.DeferredRetryDelay);
        Assert.IsTrue(leaseStore.DeferredIncrementRetryCount);
        Assert.IsNull(leaseStore.CompletedMessageId);
    }

    [TestMethod]
    public async Task RunBatchAsync_BouncesPermanentFailureDeletesRecipientsAndCompletesLease()
    {
        var identity = new MessageIdentity(13, 0, 0, 0);
        var message = CreateMessage(identity);
        var leaseStore = new FakeLeaseStore(identity);
        var messageStore = new FakeMessageStore(message);
        var resolver = new FakeTargetResolver(
            new DeliveryTargetBatch(
                new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:remote.test", "remote.test"),
                [message.Recipients[1]]));
        var dispatcher = new FakeTargetDispatcher(
            DeliveryTargetDispatchResult.PermanentFailure("550 No such user."));
        var recipientStore = new FakeRecipientStore();
        var bounceStore = new FakeBounceStore();
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            messageStore,
            resolver,
            dispatcher,
            recipientStore,
            bounceStore);

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(13, leaseStore.CompletedMessageId);
        Assert.IsNull(leaseStore.DeferredMessageId);
        Assert.AreEqual("550 No such user.", bounceStore.LastFailureDescription);
        CollectionAssert.AreEqual(new long[] { 2 }, recipientStore.DeletedRecipientIds);
    }

    [TestMethod]
    public async Task RunBatchAsync_BouncesTransientFailureWhenRetryLimitIsReached()
    {
        var identity = new MessageIdentity(14, 0, 0, 0);
        var message = CreateMessage(identity, currentRetryCount: 4);
        var leaseStore = new FakeLeaseStore(identity);
        var messageStore = new FakeMessageStore(message);
        var resolver = new FakeTargetResolver(
            new DeliveryTargetBatch(
                new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:remote.test", "remote.test"),
                [message.Recipients[1]]));
        var dispatcher = new FakeTargetDispatcher(
            DeliveryTargetDispatchResult.TransientFailure("451 Temporary failure."));
        var bounceStore = new FakeBounceStore();
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            messageStore,
            resolver,
            dispatcher,
            new FakeRecipientStore(),
            bounceStore);

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(14, leaseStore.CompletedMessageId);
        Assert.IsNull(leaseStore.DeferredMessageId);
        Assert.AreEqual("451 Temporary failure.", bounceStore.LastFailureDescription);
    }

    [TestMethod]
    public async Task RunBatchAsync_ReleasesLeaseWhenLeasedMessageCannotBeLoaded()
    {
        var identity = new MessageIdentity(12, 0, 0, 0);
        var leaseStore = new FakeLeaseStore(identity);
        var messageStore = new FakeMessageStore(message: null);
        var resolver = new FakeTargetResolver();
        var dispatcher = new FakeTargetDispatcher();
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            messageStore,
            resolver,
            dispatcher,
            new FakeRecipientStore(),
            new FakeBounceStore());

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(12, leaseStore.ReleasedMessageId);
        Assert.AreEqual(0, dispatcher.Dispatched.Count);
    }

    private static DeliveryQueueProcessorOptions CreateOptions() =>
        new(
            LeaseOwner: "test-worker",
            BatchSize: 10,
            LeaseDuration: TimeSpan.FromMinutes(5),
            RetryDelay: TimeSpan.FromMinutes(2),
            MaxRetries: 4,
            MaxRetryDelay: TimeSpan.FromHours(1));

    private static DeliveryQueuedMessage CreateMessage(MessageIdentity identity, int currentRetryCount = 0) =>
        new(
            identity,
            "queue.eml",
            "sender@example.test",
            Size: 1234,
            CreatedUtc: DateTimeOffset.Parse("2026-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture),
            Flags: ImapMessageFlags.Recent,
            CurrentRetryCount: currentRetryCount,
            Recipients:
            [
                new DeliveryQueueRecipient(1, "local@example.test", "local@example.test", LocalAccountId: 42),
                new DeliveryQueueRecipient(2, "user@remote.test", "user@remote.test", LocalAccountId: 0)
            ]);

    private sealed class FakeLeaseStore : IDeliveryQueueLeaseStore
    {
        private readonly MessageIdentity[] _identities;

        public FakeLeaseStore(params MessageIdentity[] identities)
        {
            _identities = identities;
        }

        public long? CompletedMessageId { get; private set; }

        public long? DeferredMessageId { get; private set; }

        public TimeSpan? DeferredRetryDelay { get; private set; }

        public bool DeferredIncrementRetryCount { get; private set; }

        public long? ReleasedMessageId { get; private set; }

        public async IAsyncEnumerable<MessageIdentity> LeaseReadyMessagesAsync(
            string leaseOwner,
            int batchSize,
            TimeSpan leaseDuration,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            foreach (var identity in _identities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return identity;
            }
        }

        public ValueTask<bool> CompleteAsync(
            long messageId,
            string leaseOwner,
            CancellationToken cancellationToken)
        {
            CompletedMessageId = messageId;
            return ValueTask.FromResult(true);
        }

        public ValueTask<bool> DeferAsync(
            long messageId,
            string leaseOwner,
            TimeSpan retryDelay,
            bool incrementRetryCount,
            CancellationToken cancellationToken)
        {
            DeferredMessageId = messageId;
            DeferredRetryDelay = retryDelay;
            DeferredIncrementRetryCount = incrementRetryCount;
            return ValueTask.FromResult(true);
        }

        public ValueTask<bool> ReleaseAsync(
            long messageId,
            string leaseOwner,
            CancellationToken cancellationToken)
        {
            ReleasedMessageId = messageId;
            return ValueTask.FromResult(true);
        }
    }

    private sealed class FakeMessageStore : IDeliveryQueueMessageStore
    {
        private readonly DeliveryQueuedMessage? _message;

        public FakeMessageStore(DeliveryQueuedMessage? message)
        {
            _message = message;
        }

        public ValueTask<DeliveryQueuedMessage?> TryLoadAsync(
            MessageIdentity identity,
            string leaseOwner,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_message);
    }

    private sealed class FakeTargetResolver : IDeliveryTargetResolver
    {
        private readonly IReadOnlyList<DeliveryTargetBatch> _batches;

        public FakeTargetResolver(params DeliveryTargetBatch[] batches)
        {
            _batches = batches;
        }

        public ValueTask<IReadOnlyList<DeliveryTargetBatch>> ResolveAsync(
            DeliveryQueuedMessage message,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_batches);
    }

    private sealed class FakeTargetDispatcher : IDeliveryTargetDispatcher
    {
        private readonly Queue<DeliveryTargetDispatchResult> _results;

        public FakeTargetDispatcher(params DeliveryTargetDispatchResult[] results)
        {
            _results = new Queue<DeliveryTargetDispatchResult>(results);
        }

        public List<DeliveryTargetBatch> Dispatched { get; } = [];

        public ValueTask<DeliveryTargetDispatchResult> DispatchAsync(
            DeliveryQueuedMessage message,
            DeliveryTargetBatch targetBatch,
            CancellationToken cancellationToken)
        {
            Dispatched.Add(targetBatch);
            return ValueTask.FromResult(
                _results.Count == 0
                    ? DeliveryTargetDispatchResult.Success()
                    : _results.Dequeue());
        }
    }

    private sealed class FakeRecipientStore : IDeliveryQueueRecipientStore
    {
        private readonly List<long> _deletedRecipientIds = [];

        public long[] DeletedRecipientIds => _deletedRecipientIds.ToArray();

        public ValueTask<bool> DeleteRecipientsAsync(
            long messageId,
            string leaseOwner,
            IReadOnlyList<long> recipientIds,
            CancellationToken cancellationToken)
        {
            _deletedRecipientIds.AddRange(recipientIds);
            return ValueTask.FromResult(true);
        }
    }

    private sealed class FakeBounceStore : IDeliveryBounceStore
    {
        public string? LastFailureDescription { get; private set; }

        public IReadOnlyList<DeliveryQueueRecipient>? LastFailedRecipients { get; private set; }

        public ValueTask<DeliveryBounceResult> SubmitBounceAsync(
            DeliveryQueuedMessage originalMessage,
            IReadOnlyList<DeliveryQueueRecipient> failedRecipients,
            string failureDescription,
            CancellationToken cancellationToken)
        {
            LastFailureDescription = failureDescription;
            LastFailedRecipients = failedRecipients;
            return ValueTask.FromResult(DeliveryBounceResult.SubmittedResult());
        }
    }
}
