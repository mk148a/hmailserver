using System.Runtime.CompilerServices;
using System.Text;
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
        var statusObserver = new FakeStatusObserver();
        var contentStore = new FakeMessageContentStore("Subject: Delivery\r\n\r\nBody\r\n");
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            messageStore,
            resolver,
            dispatcher,
            recipientStore,
            new FakeBounceStore(),
            messageContentStore: contentStore,
            statusObserver: statusObserver);

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(2, dispatcher.Dispatched.Count);
        CollectionAssert.AreEqual(new long[] { 1, 2 }, recipientStore.DeletedRecipientIds);
        Assert.AreEqual(10, leaseStore.CompletedMessageId);
        Assert.IsNull(leaseStore.DeferredMessageId);
        Assert.AreEqual(10, contentStore.DeletedMessages.Single().Identity.MessageId);
        CollectionAssert.AreEqual(
            new[]
            {
                DeliveryQueueStatusEventKind.MessageLeased,
                DeliveryQueueStatusEventKind.TargetDeliverySucceeded,
                DeliveryQueueStatusEventKind.TargetDeliverySucceeded,
                DeliveryQueueStatusEventKind.MessageCompleted
            },
            statusObserver.Kinds);
        Assert.AreEqual("local:42", statusObserver.Events[1].TargetKey);
        Assert.AreEqual("remote:remote.test", statusObserver.Events[2].TargetKey);
    }

    [TestMethod]
    public async Task RunBatchAsync_SignsAfterOnDeliverMessageBeforeResolutionAndDispatch()
    {
        var identity = new MessageIdentity(101, 0, 0, 0);
        var message = CreateMessage(identity);
        var order = new List<string>();
        var leaseStore = new FakeLeaseStore(identity);
        var contentStore = new FakeMessageContentStore("From: sender@example.test\r\n\r\nBody\r\n")
        {
            Order = order
        };
        var messageStore = new FakeMessageStore(message)
        {
            Order = order
        };
        var eventExecutor = new FakeDeliveryEventScriptExecutor(request =>
        {
            order.Add(request.EventName);
            return DeliveryEventScriptExecutionResult.Continue(request.MessageData);
        });
        var resolver = new FakeTargetResolver(
            new DeliveryTargetBatch(
                new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:remote.test", "remote.test"),
                message.Recipients))
        {
            OnResolve = _ => order.Add("Resolve")
        };
        var signer = new RecordingDkimSigner(order);
        var dispatcher = new FakeTargetDispatcher(DeliveryTargetDispatchResult.Success())
        {
            Order = order
        };
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            messageStore,
            resolver,
            dispatcher,
            new FakeRecipientStore(),
            new FakeBounceStore(),
            eventExecutor,
            contentStore,
            dkimSigner: signer);

        await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.IsTrue(order.IndexOf("OnDeliverMessage") < order.IndexOf("Signer"));
        Assert.IsTrue(order.IndexOf("Signer") < order.IndexOf("ContentSave"));
        Assert.IsTrue(order.IndexOf("ContentSave") < order.IndexOf("SizeUpdate"));
        Assert.IsTrue(order.IndexOf("SizeUpdate") < order.IndexOf("Resolve"));
        Assert.IsTrue(order.IndexOf("Resolve") < order.IndexOf("Dispatch"));
        StringAssert.Contains(contentStore.Text, "X-DKIM-Test: signed");
        Assert.AreEqual(contentStore.Text, Encoding.ASCII.GetString(signer.LastBytes!));
        Assert.AreEqual(contentStore.Bytes.LongLength, messageStore.UpdatedSize);
        Assert.AreEqual(1, dispatcher.Messages.Count);
    }

    [TestMethod]
    public async Task RunBatchAsync_DoesNotInvokeSignerForRetry()
    {
        var identity = new MessageIdentity(102, 0, 0, 0);
        var message = CreateMessage(identity, currentRetryCount: 1);
        var signer = new RecordingDkimSigner([]);
        var contentStore = new FakeMessageContentStore("From: sender@example.test\r\n\r\nBody\r\n");
        var messageStore = new FakeMessageStore(message);
        var resolver = new FakeTargetResolver(
            new DeliveryTargetBatch(
                new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:remote.test", "remote.test"),
                message.Recipients));
        var processor = new DeliveryQueueProcessor(
            new FakeLeaseStore(identity),
            messageStore,
            resolver,
            new FakeTargetDispatcher(DeliveryTargetDispatchResult.Success()),
            new FakeRecipientStore(),
            new FakeBounceStore(),
            messageContentStore: contentStore,
            dkimSigner: signer);

        await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(0, signer.CallCount);
        Assert.AreEqual(0, messageStore.UpdateSizeCallCount);
        Assert.AreEqual("From: sender@example.test\r\n\r\nBody\r\n", contentStore.Text);
    }

    [TestMethod]
    public async Task RunBatchAsync_DefersWhenSignedMessageSizeCannotBePersisted()
    {
        var identity = new MessageIdentity(103, 0, 0, 0);
        var message = CreateMessage(identity);
        var leaseStore = new FakeLeaseStore(identity);
        var messageStore = new FakeMessageStore(message) { UpdateSizeResult = false };
        var resolver = new FakeTargetResolver(
            new DeliveryTargetBatch(
                new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:remote.test", "remote.test"),
                message.Recipients));
        var dispatcher = new FakeTargetDispatcher(DeliveryTargetDispatchResult.Success());
        var contentStore = new FakeMessageContentStore("From: sender@example.test\r\n\r\nBody\r\n");
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            messageStore,
            resolver,
            dispatcher,
            new FakeRecipientStore(),
            new FakeBounceStore(),
            messageContentStore: contentStore,
            dkimSigner: new RecordingDkimSigner([]));

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(1, messageStore.UpdateSizeCallCount);
        Assert.AreEqual(identity.MessageId, leaseStore.DeferredMessageId);
        Assert.AreEqual(TimeSpan.FromMinutes(2), leaseStore.DeferredRetryDelay);
        Assert.IsNull(leaseStore.CompletedMessageId);
        Assert.AreEqual(0, dispatcher.Dispatched.Count);
        Assert.IsEmpty(contentStore.DeletedMessages);
    }

    [TestMethod]
    public async Task RunBatchAsync_DefersWhenSignedMessageSizePersistenceThrows()
    {
        var identity = new MessageIdentity(104, 0, 0, 0);
        var message = CreateMessage(identity);
        var leaseStore = new FakeLeaseStore(identity);
        var messageStore = new FakeMessageStore(message)
        {
            UpdateSizeException = new IOException("size update failed")
        };
        var resolver = new FakeTargetResolver(
            new DeliveryTargetBatch(
                new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:remote.test", "remote.test"),
                message.Recipients));
        var dispatcher = new FakeTargetDispatcher(DeliveryTargetDispatchResult.Success());
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            messageStore,
            resolver,
            dispatcher,
            new FakeRecipientStore(),
            new FakeBounceStore(),
            messageContentStore: new FakeMessageContentStore("From: sender@example.test\r\n\r\nBody\r\n"),
            dkimSigner: new RecordingDkimSigner([]));

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(identity.MessageId, leaseStore.DeferredMessageId);
        Assert.IsNull(leaseStore.CompletedMessageId);
        Assert.AreEqual(0, dispatcher.Dispatched.Count);
    }

    [TestMethod]
    public async Task RunBatchAsync_PropagatesCancellationFromSignedMessageSizePersistence()
    {
        var identity = new MessageIdentity(105, 0, 0, 0);
        var message = CreateMessage(identity);
        using var cancellation = new CancellationTokenSource();
        var leaseStore = new FakeLeaseStore(identity);
        var messageStore = new FakeMessageStore(message) { CancelOnUpdate = cancellation };
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            messageStore,
            new FakeTargetResolver(
                new DeliveryTargetBatch(
                    new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:remote.test", "remote.test"),
                    message.Recipients)),
            new FakeTargetDispatcher(DeliveryTargetDispatchResult.Success()),
            new FakeRecipientStore(),
            new FakeBounceStore(),
            messageContentStore: new FakeMessageContentStore("From: sender@example.test\r\n\r\nBody\r\n"),
            dkimSigner: new RecordingDkimSigner([]));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => processor.RunBatchAsync(CreateOptions(), cancellation.Token).AsTask());

        Assert.IsNull(leaseStore.DeferredMessageId);
        Assert.IsNull(leaseStore.CompletedMessageId);
    }

    [TestMethod]
    public async Task RunBatchAsync_DefersWhenDeliveryEventSizeCannotBePersisted()
    {
        var identity = new MessageIdentity(106, 0, 0, 0);
        var message = CreateMessage(identity);
        var leaseStore = new FakeLeaseStore(identity);
        var messageStore = new FakeMessageStore(message) { UpdateSizeResult = false };
        var resolver = new FakeTargetResolver(
            new DeliveryTargetBatch(
                new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:remote.test", "remote.test"),
                message.Recipients));
        var dispatcher = new FakeTargetDispatcher(DeliveryTargetDispatchResult.Success());
        var contentStore = new FakeMessageContentStore("From: sender@example.test\r\n\r\nBody\r\n");
        var eventExecutor = new FakeDeliveryEventScriptExecutor(request =>
            request.EventName == "OnDeliverMessage"
                ? DeliveryEventScriptExecutionResult.Continue(
                    Encoding.ASCII.GetBytes("From: sender@example.test\r\n\r\nMutated\r\n"))
                : DeliveryEventScriptExecutionResult.Continue(request.MessageData));
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            messageStore,
            resolver,
            dispatcher,
            new FakeRecipientStore(),
            new FakeBounceStore(),
            eventExecutor,
            contentStore);

        await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(identity.MessageId, leaseStore.DeferredMessageId);
        Assert.IsNull(leaseStore.CompletedMessageId);
        Assert.AreEqual(0, dispatcher.Dispatched.Count);
        Assert.AreEqual(1, messageStore.UpdateSizeCallCount);
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
        var statusObserver = new FakeStatusObserver();
        var contentStore = new FakeMessageContentStore("Subject: Delivery\r\n\r\nBody\r\n");
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            messageStore,
            resolver,
            dispatcher,
            new FakeRecipientStore(),
            new FakeBounceStore(),
            messageContentStore: contentStore,
            statusObserver: statusObserver);

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(11, leaseStore.DeferredMessageId);
        Assert.AreEqual(TimeSpan.FromSeconds(30), leaseStore.DeferredRetryDelay);
        Assert.IsTrue(leaseStore.DeferredIncrementRetryCount);
        Assert.IsNull(leaseStore.CompletedMessageId);
        Assert.IsEmpty(contentStore.DeletedMessages);
        CollectionAssert.AreEqual(
            new[]
            {
                DeliveryQueueStatusEventKind.MessageLeased,
                DeliveryQueueStatusEventKind.TargetDeliveryDeferred,
                DeliveryQueueStatusEventKind.MessageDeferred
            },
            statusObserver.Kinds);
        Assert.AreEqual(TimeSpan.FromSeconds(30), statusObserver.Events[1].RetryDelay);
        Assert.AreEqual(DeliveryFailureKind.Transient, statusObserver.Events[1].FailureKind);
        Assert.AreEqual("Remote temporary failure.", statusObserver.Events[1].Description);
    }

    [TestMethod]
    public async Task RunBatchAsync_DeletesAcceptedRecipientAndDefersOnlyTransientRecipient()
    {
        var identity = new MessageIdentity(24, 0, 0, 0);
        var message = CreateMessage(identity);
        var leaseStore = new FakeLeaseStore(identity);
        var recipientStore = new FakeRecipientStore();
        var resolver = new FakeTargetResolver(
            new DeliveryTargetBatch(
                new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:remote.test", "remote.test"),
                message.Recipients));
        var dispatcher = new FakeTargetDispatcher(
            DeliveryTargetDispatchResult.TransientFailure(
                "451 user temporarily deferred",
                TimeSpan.FromMinutes(3),
                [
                    new DeliveryRecipientDispatchResult(1, null, null),
                    new DeliveryRecipientDispatchResult(2, DeliveryFailureKind.Transient, "451 user temporarily deferred")
                ]));
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            new FakeMessageStore(message),
            resolver,
            dispatcher,
            recipientStore,
            new FakeBounceStore());

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        CollectionAssert.AreEqual(new long[] { 1 }, recipientStore.DeletedRecipientIds);
        Assert.AreEqual(identity.MessageId, leaseStore.DeferredMessageId);
        Assert.AreEqual(TimeSpan.FromMinutes(3), leaseStore.DeferredRetryDelay);
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
        var statusObserver = new FakeStatusObserver();
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            messageStore,
            resolver,
            dispatcher,
            recipientStore,
            bounceStore,
            statusObserver: statusObserver);

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(13, leaseStore.CompletedMessageId);
        Assert.IsNull(leaseStore.DeferredMessageId);
        Assert.AreEqual("550 No such user.", bounceStore.LastFailureDescription);
        CollectionAssert.AreEqual(new long[] { 2 }, recipientStore.DeletedRecipientIds);
        CollectionAssert.AreEqual(
            new[]
            {
                DeliveryQueueStatusEventKind.MessageLeased,
                DeliveryQueueStatusEventKind.TargetDeliveryFailedPermanently,
                DeliveryQueueStatusEventKind.BounceSubmitted,
                DeliveryQueueStatusEventKind.MessageCompleted
            },
            statusObserver.Kinds);
        Assert.AreEqual(DeliveryFailureKind.Permanent, statusObserver.Events[1].FailureKind);
        Assert.AreEqual(1, statusObserver.Events[2].RecipientCount);
    }

    [TestMethod]
    public async Task RunBatchAsync_RecordsBounceSkippedReasonAndCompletesLease()
    {
        var identity = new MessageIdentity(19, 0, 0, 0);
        var message = CreateMessage(identity);
        var leaseStore = new FakeLeaseStore(identity);
        var resolver = new FakeTargetResolver(
            new DeliveryTargetBatch(
                new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:remote.test", "remote.test"),
                [message.Recipients[1]]));
        var dispatcher = new FakeTargetDispatcher(
            DeliveryTargetDispatchResult.PermanentFailure("550 No such user."));
        var recipientStore = new FakeRecipientStore();
        var bounceStore = new FakeBounceStore(
            DeliveryBounceResult.Skipped("Original sender is already a mailer daemon address."));
        var statusObserver = new FakeStatusObserver();
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            new FakeMessageStore(message),
            resolver,
            dispatcher,
            recipientStore,
            bounceStore,
            statusObserver: statusObserver);

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(19, leaseStore.CompletedMessageId);
        CollectionAssert.AreEqual(new long[] { 2 }, recipientStore.DeletedRecipientIds);
        CollectionAssert.AreEqual(
            new[]
            {
                DeliveryQueueStatusEventKind.MessageLeased,
                DeliveryQueueStatusEventKind.TargetDeliveryFailedPermanently,
                DeliveryQueueStatusEventKind.BounceSkipped,
                DeliveryQueueStatusEventKind.MessageCompleted
            },
            statusObserver.Kinds);
        Assert.AreEqual("Original sender is already a mailer daemon address.", statusObserver.Events[2].Description);
        Assert.AreEqual(1, statusObserver.Events[2].RecipientCount);
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
    public async Task RunBatchAsync_DropsMessageWhenOnDeliveryStartRequestsDrop()
    {
        var identity = new MessageIdentity(15, 0, 0, 0);
        var message = CreateMessage(identity);
        var leaseStore = new FakeLeaseStore(identity);
        var dispatcher = new FakeTargetDispatcher();
        var eventExecutor = new FakeDeliveryEventScriptExecutor(
            DeliveryEventScriptExecutionResult.Drop());
        var contentStore = new FakeMessageContentStore("Subject: Delivery\r\n\r\nBody\r\n");
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            new FakeMessageStore(message),
            new FakeTargetResolver(
                new DeliveryTargetBatch(
                    new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:remote.test", "remote.test"),
                    message.Recipients)),
            dispatcher,
            new FakeRecipientStore(),
            new FakeBounceStore(),
            eventExecutor,
            contentStore);

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(15, leaseStore.CompletedMessageId);
        Assert.IsNull(leaseStore.DeferredMessageId);
        Assert.AreEqual(0, dispatcher.Dispatched.Count);
        Assert.AreEqual(15, contentStore.DeletedMessages.Single().Identity.MessageId);
        CollectionAssert.AreEqual(new[] { "OnDeliveryStart" }, eventExecutor.EventNames);
    }

    [TestMethod]
    public async Task RunBatchAsync_DropsContentWhenOnDeliverMessageRequestsDrop()
    {
        var identity = new MessageIdentity(20, 0, 0, 0);
        var message = CreateMessage(identity);
        var leaseStore = new FakeLeaseStore(identity);
        var dispatcher = new FakeTargetDispatcher();
        var eventExecutor = new FakeDeliveryEventScriptExecutor(
            DeliveryEventScriptExecutionResult.Continue("Subject: Delivery\r\n\r\nBody\r\n"u8.ToArray()),
            DeliveryEventScriptExecutionResult.Drop());
        var contentStore = new FakeMessageContentStore("Subject: Delivery\r\n\r\nBody\r\n");
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            new FakeMessageStore(message),
            new FakeTargetResolver(
                new DeliveryTargetBatch(
                    new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:remote.test", "remote.test"),
                    message.Recipients)),
            dispatcher,
            new FakeRecipientStore(),
            new FakeBounceStore(),
            eventExecutor,
            contentStore);

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(20, leaseStore.CompletedMessageId);
        Assert.AreEqual(20, contentStore.DeletedMessages.Single().Identity.MessageId);
        Assert.AreEqual(0, dispatcher.Dispatched.Count);
        CollectionAssert.AreEqual(
            new[] { "OnDeliveryStart", "OnDeliverMessage" },
            eventExecutor.EventNames);
    }

    [TestMethod]
    public async Task RunBatchAsync_DeletesContentWhenNoDeliveryTargetsRemain()
    {
        var identity = new MessageIdentity(21, 0, 0, 0);
        var message = CreateMessage(identity);
        var leaseStore = new FakeLeaseStore(identity);
        var contentStore = new FakeMessageContentStore("Subject: Delivery\r\n\r\nBody\r\n");
        var statusObserver = new FakeStatusObserver();
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            new FakeMessageStore(message),
            new FakeTargetResolver(),
            new FakeTargetDispatcher(),
            new FakeRecipientStore(),
            new FakeBounceStore(),
            messageContentStore: contentStore,
            statusObserver: statusObserver);

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(21, leaseStore.CompletedMessageId);
        Assert.AreEqual(21, contentStore.DeletedMessages.Single().Identity.MessageId);
        CollectionAssert.AreEqual(
            new[]
            {
                DeliveryQueueStatusEventKind.MessageLeased,
                DeliveryQueueStatusEventKind.NoDeliveryTargets,
                DeliveryQueueStatusEventKind.MessageCompleted
            },
            statusObserver.Kinds);
    }

    [TestMethod]
    public async Task RunBatchAsync_DoesNotDeleteContentWhenCompletionLosesLease()
    {
        var identity = new MessageIdentity(22, 0, 0, 0);
        var message = CreateMessage(identity);
        var leaseStore = new FakeLeaseStore(identity) { CompleteResult = false };
        var contentStore = new FakeMessageContentStore("Subject: Delivery\r\n\r\nBody\r\n");
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            new FakeMessageStore(message),
            new FakeTargetResolver(),
            new FakeTargetDispatcher(),
            new FakeRecipientStore(),
            new FakeBounceStore(),
            messageContentStore: contentStore);

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(22, leaseStore.CompletedMessageId);
        Assert.IsEmpty(contentStore.DeletedMessages);
    }

    [TestMethod]
    public async Task RunBatchAsync_IgnoresContentDeleteFailureAfterCompletion()
    {
        var identity = new MessageIdentity(23, 0, 0, 0);
        var message = CreateMessage(identity);
        var leaseStore = new FakeLeaseStore(identity);
        var contentStore = new FakeMessageContentStore("Subject: Delivery\r\n\r\nBody\r\n")
        {
            DeleteException = new IOException("file is busy")
        };
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            new FakeMessageStore(message),
            new FakeTargetResolver(),
            new FakeTargetDispatcher(),
            new FakeRecipientStore(),
            new FakeBounceStore(),
            messageContentStore: contentStore);

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(23, leaseStore.CompletedMessageId);
        Assert.IsNull(leaseStore.DeferredMessageId);
    }

    [TestMethod]
    public async Task RunBatchAsync_RunsDeliveryEventsBeforeDispatchAndPersistsMutations()
    {
        var identity = new MessageIdentity(16, 0, 0, 0);
        var message = CreateMessage(identity);
        var leaseStore = new FakeLeaseStore(identity);
        var dispatcher = new FakeTargetDispatcher(DeliveryTargetDispatchResult.Success());
        var contentStore = new FakeMessageContentStore("Subject: Delivery\r\n\r\nBody\r\n");
        var eventExecutor = new FakeDeliveryEventScriptExecutor(
            request => DeliveryEventScriptExecutionResult.Continue(
                Encoding.ASCII.GetBytes(
                    Encoding.ASCII.GetString(request.MessageData) +
                    "X-" + request.EventName + ": yes\r\n")));
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            new FakeMessageStore(message),
            new FakeTargetResolver(
                new DeliveryTargetBatch(
                    new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:remote.test", "remote.test"),
                    [message.Recipients[1]])),
            dispatcher,
            new FakeRecipientStore(),
            new FakeBounceStore(),
            eventExecutor,
            contentStore);

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        CollectionAssert.AreEqual(new[] { "OnDeliveryStart", "OnDeliverMessage" }, eventExecutor.EventNames);
        Assert.IsTrue(eventExecutor.Requests.All(request => request.MessageState == 1));
        Assert.IsTrue(eventExecutor.Requests.All(request => request.MessageFlags == ImapMessageFlags.Recent));
        StringAssert.Contains(contentStore.Text, "X-OnDeliveryStart: yes");
        StringAssert.Contains(contentStore.Text, "X-OnDeliverMessage: yes");
        Assert.AreEqual(contentStore.Bytes.LongLength, dispatcher.Messages[0].Size);
    }

    [TestMethod]
    public async Task RunBatchAsync_DefersLeaseWhenDeliveryEventFails()
    {
        var identity = new MessageIdentity(17, 0, 0, 0);
        var message = CreateMessage(identity);
        var leaseStore = new FakeLeaseStore(identity);
        var dispatcher = new FakeTargetDispatcher();
        var statusObserver = new FakeStatusObserver();
        var contentStore = new FakeMessageContentStore("Subject: Delivery\r\n\r\nBody\r\n");
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            new FakeMessageStore(message),
            new FakeTargetResolver(
                new DeliveryTargetBatch(
                    new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:remote.test", "remote.test"),
                    [message.Recipients[1]])),
            dispatcher,
            new FakeRecipientStore(),
            new FakeBounceStore(),
            new FakeDeliveryEventScriptExecutor(
                DeliveryEventScriptExecutionResult.Failure("Script failed.")),
            contentStore,
            statusObserver);

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(17, leaseStore.DeferredMessageId);
        Assert.AreEqual(TimeSpan.FromMinutes(2), leaseStore.DeferredRetryDelay);
        Assert.IsTrue(leaseStore.DeferredIncrementRetryCount);
        Assert.IsNull(leaseStore.CompletedMessageId);
        Assert.IsEmpty(contentStore.DeletedMessages);
        Assert.AreEqual(0, dispatcher.Dispatched.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                DeliveryQueueStatusEventKind.MessageLeased,
                DeliveryQueueStatusEventKind.DeliveryEventFailed,
                DeliveryQueueStatusEventKind.MessageDeferred
            },
            statusObserver.Kinds);
        Assert.AreEqual(TimeSpan.FromMinutes(2), statusObserver.Events[1].RetryDelay);
        Assert.AreEqual("OnDeliveryStart failed: Script failed.", statusObserver.Events[1].Description);
    }

    [TestMethod]
    public async Task RunBatchAsync_RunsDeliveryFailedEventBeforeBouncingRecipients()
    {
        var identity = new MessageIdentity(18, 0, 0, 0);
        var message = CreateMessage(identity);
        var leaseStore = new FakeLeaseStore(identity);
        var eventExecutor = new FakeDeliveryEventScriptExecutor(
            request => DeliveryEventScriptExecutionResult.Continue(request.MessageData));
        var bounceStore = new FakeBounceStore();
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            new FakeMessageStore(message),
            new FakeTargetResolver(
                new DeliveryTargetBatch(
                    new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:remote.test", "remote.test"),
                    [message.Recipients[1]])),
            new FakeTargetDispatcher(
                DeliveryTargetDispatchResult.PermanentFailure("550 No such user.")),
            new FakeRecipientStore(),
            bounceStore,
            eventExecutor,
            new FakeMessageContentStore("Subject: Delivery\r\n\r\nBody\r\n"));

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        CollectionAssert.AreEqual(
            new[] { "OnDeliveryStart", "OnDeliverMessage", "OnDeliveryFailed" },
            eventExecutor.EventNames);
        var failedEvent = eventExecutor.Requests.Single(request => request.EventName == "OnDeliveryFailed");
        Assert.AreEqual("user@remote.test", failedEvent.RecipientAddress);
        Assert.AreEqual("550 No such user.", failedEvent.ErrorMessage);
        Assert.AreEqual(1, failedEvent.MessageState);
        Assert.AreEqual(ImapMessageFlags.Recent, failedEvent.MessageFlags);
        Assert.AreEqual("550 No such user.", bounceStore.LastFailureDescription);
        Assert.AreEqual(18, leaseStore.CompletedMessageId);
    }

    [TestMethod]
    public async Task RunBatchAsync_ReleasesLeaseWhenLeasedMessageCannotBeLoaded()
    {
        var identity = new MessageIdentity(12, 0, 0, 0);
        var leaseStore = new FakeLeaseStore(identity);
        var messageStore = new FakeMessageStore(message: null);
        var resolver = new FakeTargetResolver();
        var dispatcher = new FakeTargetDispatcher();
        var statusObserver = new FakeStatusObserver();
        var contentStore = new FakeMessageContentStore("Subject: Delivery\r\n\r\nBody\r\n");
        var processor = new DeliveryQueueProcessor(
            leaseStore,
            messageStore,
            resolver,
            dispatcher,
            new FakeRecipientStore(),
            new FakeBounceStore(),
            messageContentStore: contentStore,
            statusObserver: statusObserver);

        var processed = await processor.RunBatchAsync(CreateOptions(), CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(12, leaseStore.ReleasedMessageId);
        Assert.AreEqual(0, dispatcher.Dispatched.Count);
        Assert.IsEmpty(contentStore.DeletedMessages);
        CollectionAssert.AreEqual(
            new[]
            {
                DeliveryQueueStatusEventKind.MessageLeased,
                DeliveryQueueStatusEventKind.MessageLoadMissing,
                DeliveryQueueStatusEventKind.MessageReleased
            },
            statusObserver.Kinds);
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

        public bool CompleteResult { get; set; } = true;

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
            return ValueTask.FromResult(CompleteResult);
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

        public List<string>? Order { get; init; }

        public bool UpdateSizeResult { get; init; } = true;

        public Exception? UpdateSizeException { get; init; }

        public CancellationTokenSource? CancelOnUpdate { get; init; }

        public int UpdateSizeCallCount { get; private set; }

        public long? UpdatedSize { get; private set; }

        public ValueTask<DeliveryQueuedMessage?> TryLoadAsync(
            MessageIdentity identity,
            string leaseOwner,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_message);

        public ValueTask<bool> TryUpdateSizeAsync(
            DeliveryQueuedMessage message,
            long size,
            string leaseOwner,
            CancellationToken cancellationToken)
        {
            UpdateSizeCallCount++;
            UpdatedSize = size;
            Order?.Add("SizeUpdate");
            if (CancelOnUpdate is not null)
            {
                CancelOnUpdate.Cancel();
                return ValueTask.FromException<bool>(new OperationCanceledException(CancelOnUpdate.Token));
            }

            if (UpdateSizeException is not null)
            {
                return ValueTask.FromException<bool>(UpdateSizeException);
            }

            return ValueTask.FromResult(UpdateSizeResult);
        }
    }

    private sealed class FakeTargetResolver : IDeliveryTargetResolver
    {
        private readonly IReadOnlyList<DeliveryTargetBatch> _batches;

        public FakeTargetResolver(params DeliveryTargetBatch[] batches)
        {
            _batches = batches;
        }

        public Action<DeliveryQueuedMessage>? OnResolve { get; init; }

        public ValueTask<IReadOnlyList<DeliveryTargetBatch>> ResolveAsync(
            DeliveryQueuedMessage message,
            CancellationToken cancellationToken)
        {
            OnResolve?.Invoke(message);
            return ValueTask.FromResult(_batches);
        }
    }

    private sealed class FakeTargetDispatcher : IDeliveryTargetDispatcher
    {
        private readonly Queue<DeliveryTargetDispatchResult> _results;

        public FakeTargetDispatcher(params DeliveryTargetDispatchResult[] results)
        {
            _results = new Queue<DeliveryTargetDispatchResult>(results);
        }

        public List<DeliveryTargetBatch> Dispatched { get; } = [];

        public List<DeliveryQueuedMessage> Messages { get; } = [];

        public List<string>? Order { get; init; }

        public ValueTask<DeliveryTargetDispatchResult> DispatchAsync(
            DeliveryQueuedMessage message,
            DeliveryTargetBatch targetBatch,
            CancellationToken cancellationToken)
        {
            Order?.Add("Dispatch");
            Messages.Add(message);
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
        private readonly DeliveryBounceResult _result;

        public FakeBounceStore(DeliveryBounceResult? result = null)
        {
            _result = result ?? DeliveryBounceResult.SubmittedResult();
        }

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
            return ValueTask.FromResult(_result);
        }
    }

    private sealed class FakeStatusObserver : IDeliveryQueueStatusObserver
    {
        private readonly List<DeliveryQueueStatusEvent> _events = [];

        public IReadOnlyList<DeliveryQueueStatusEvent> Events => _events;

        public DeliveryQueueStatusEventKind[] Kinds =>
            _events.Select(static statusEvent => statusEvent.Kind).ToArray();

        public ValueTask RecordAsync(
            DeliveryQueueStatusEvent statusEvent,
            CancellationToken cancellationToken)
        {
            _events.Add(statusEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeDeliveryEventScriptExecutor : IDeliveryEventScriptExecutor
    {
        private readonly Queue<DeliveryEventScriptExecutionResult>? _results;
        private readonly Func<DeliveryEventScriptExecutionRequest, DeliveryEventScriptExecutionResult>? _handler;
        private readonly List<string> _eventNames = [];
        private readonly List<DeliveryEventScriptExecutionRequest> _requests = [];

        public FakeDeliveryEventScriptExecutor(params DeliveryEventScriptExecutionResult[] results)
        {
            _results = new Queue<DeliveryEventScriptExecutionResult>(results);
        }

        public FakeDeliveryEventScriptExecutor(
            Func<DeliveryEventScriptExecutionRequest, DeliveryEventScriptExecutionResult> handler)
        {
            _handler = handler;
        }

        public string[] EventNames => _eventNames.ToArray();

        public IReadOnlyList<DeliveryEventScriptExecutionRequest> Requests => _requests;

        public DeliveryEventScriptExecutionResult Execute(
            DeliveryEventScriptExecutionRequest request,
            CancellationToken cancellationToken)
        {
            _eventNames.Add(request.EventName);
            _requests.Add(request);
            if (_handler is not null)
            {
                return _handler(request);
            }

            return _results is { Count: > 0 }
                ? _results.Dequeue()
                : DeliveryEventScriptExecutionResult.Continue(request.MessageData);
        }
    }

    private sealed class FakeMessageContentStore : IDeliveryMessageContentStore
    {
        public FakeMessageContentStore(string text)
        {
            Bytes = Encoding.ASCII.GetBytes(text);
        }

        public byte[] Bytes { get; private set; }

        public string Text => Encoding.ASCII.GetString(Bytes);

        public List<DeliveryQueuedMessage> DeletedMessages { get; } = [];

        public Exception? DeleteException { get; init; }

        public List<string>? Order { get; init; }

        public ValueTask<byte[]?> TryLoadAsync(
            DeliveryQueuedMessage message,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<byte[]?>(Bytes);

        public ValueTask<bool> TrySaveAsync(
            DeliveryQueuedMessage message,
            byte[] messageData,
            CancellationToken cancellationToken)
        {
            Order?.Add("ContentSave");
            Bytes = messageData;
            return ValueTask.FromResult(true);
        }

        public ValueTask<bool> TryDeleteAsync(
            DeliveryQueuedMessage message,
            CancellationToken cancellationToken)
        {
            if (DeleteException is not null)
            {
                return ValueTask.FromException<bool>(DeleteException);
            }

            DeletedMessages.Add(message);
            return ValueTask.FromResult(true);
        }
    }

    private sealed class RecordingDkimSigner(List<string> order) : IDkimSigner
    {
        public int CallCount { get; private set; }
        public byte[]? LastBytes { get; private set; }

        public ValueTask<byte[]?> SignAsync(
            DeliveryQueuedMessage message,
            byte[] messageData,
            CancellationToken cancellationToken)
        {
            CallCount++;
            order.Add("Signer");
            LastBytes = Encoding.ASCII.GetBytes("X-DKIM-Test: signed\r\n" + Encoding.ASCII.GetString(messageData));
            return ValueTask.FromResult<byte[]?>(LastBytes);
        }
    }
}
