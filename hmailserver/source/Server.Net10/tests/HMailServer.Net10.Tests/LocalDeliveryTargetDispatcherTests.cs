using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class LocalDeliveryTargetDispatcherTests
{
    [TestMethod]
    public async Task DispatchAsync_HandsLocalBatchToLocalDeliveryStore()
    {
        var message = CreateMessage();
        var batch = new DeliveryTargetBatch(
            new DeliveryTarget(DeliveryTargetKind.LocalAccount, "local:42", "example.test", LocalAccountId: 42),
            message.Recipients);
        var store = new FakeLocalDeliveryStore();
        var dispatcher = new LocalDeliveryTargetDispatcher(store);

        var result = await dispatcher.DispatchAsync(message, batch, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreSame(message, store.LastMessage);
        Assert.AreSame(batch, store.LastBatch);
    }

    [TestMethod]
    public async Task DispatchAsync_DefersNonLocalBatchUntilRemoteDispatcherExists()
    {
        var message = CreateMessage();
        var batch = new DeliveryTargetBatch(
            new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:example.net", "example.net"),
            message.Recipients);
        var dispatcher = new LocalDeliveryTargetDispatcher(new FakeLocalDeliveryStore());

        var result = await dispatcher.DispatchAsync(message, batch, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error, "not handled");
    }

    private static DeliveryQueuedMessage CreateMessage() =>
        new(
            new MessageIdentity(100, 0, 0, 0),
            "queue.eml",
            "sender@example.test",
            Size: 1234,
            CreatedUtc: DateTimeOffset.Parse("2026-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture),
            Flags: ImapMessageFlags.Recent,
            CurrentRetryCount: 0,
            Recipients:
            [
                new DeliveryQueueRecipient(1, "user@example.test", "user@example.test", LocalAccountId: 42)
            ]);

    private sealed class FakeLocalDeliveryStore : ILocalDeliveryStore
    {
        public DeliveryQueuedMessage? LastMessage { get; private set; }

        public DeliveryTargetBatch? LastBatch { get; private set; }

        public ValueTask<LocalDeliveryResult> DeliverAsync(
            DeliveryQueuedMessage message,
            DeliveryTargetBatch targetBatch,
            CancellationToken cancellationToken)
        {
            LastMessage = message;
            LastBatch = targetBatch;
            return ValueTask.FromResult(
                new LocalDeliveryResult(
                    new MessageIdentity(200, 42, 5, 9),
                    targetBatch.Recipients.Count));
        }
    }
}
