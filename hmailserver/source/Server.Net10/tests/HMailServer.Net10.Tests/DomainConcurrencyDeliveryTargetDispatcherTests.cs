using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DomainConcurrencyDeliveryTargetDispatcherTests
{
    [TestMethod]
    public async Task DispatchAsync_LimitsConcurrentDeliveriesForSameDomain()
    {
        var inner = new MeasuringDispatcher(delay: TimeSpan.FromMilliseconds(60));
        var dispatcher = new DomainConcurrencyDeliveryTargetDispatcher(
            inner,
            new DomainConcurrencyOptions(MaxConcurrentDeliveriesPerDomain: 1));
        var message = CreateMessage();
        var batch = CreateBatch("example.net");

        await Task.WhenAll(
            dispatcher.DispatchAsync(message, batch, CancellationToken.None).AsTask(),
            dispatcher.DispatchAsync(message, batch, CancellationToken.None).AsTask());

        Assert.AreEqual(2, inner.CallCount);
        Assert.AreEqual(1, inner.MaxObservedConcurrency);
    }

    [TestMethod]
    public async Task DispatchAsync_AllowsDifferentDomainsInParallel()
    {
        var inner = new MeasuringDispatcher(delay: TimeSpan.FromMilliseconds(80));
        var dispatcher = new DomainConcurrencyDeliveryTargetDispatcher(
            inner,
            new DomainConcurrencyOptions(MaxConcurrentDeliveriesPerDomain: 1));
        var message = CreateMessage();

        await Task.WhenAll(
            dispatcher.DispatchAsync(message, CreateBatch("one.example"), CancellationToken.None).AsTask(),
            dispatcher.DispatchAsync(message, CreateBatch("two.example"), CancellationToken.None).AsTask());

        Assert.AreEqual(2, inner.CallCount);
        Assert.AreEqual(2, inner.MaxObservedConcurrency);
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
                new DeliveryQueueRecipient(1, "user@example.net", "user@example.net", LocalAccountId: 0)
            ]);

    private static DeliveryTargetBatch CreateBatch(string domainName) =>
        new(
            new DeliveryTarget(
                DeliveryTargetKind.RemoteDomain,
                "remote:" + domainName,
                domainName),
            [new DeliveryQueueRecipient(1, "user@" + domainName, "user@" + domainName, LocalAccountId: 0)]);

    private sealed class MeasuringDispatcher : IDeliveryTargetDispatcher
    {
        private readonly TimeSpan _delay;
        private int _active;

        public MeasuringDispatcher(TimeSpan delay)
        {
            _delay = delay;
        }

        public int CallCount { get; private set; }

        public int MaxObservedConcurrency { get; private set; }

        public async ValueTask<DeliveryTargetDispatchResult> DispatchAsync(
            DeliveryQueuedMessage message,
            DeliveryTargetBatch targetBatch,
            CancellationToken cancellationToken)
        {
            CallCount++;
            var active = Interlocked.Increment(ref _active);
            MaxObservedConcurrency = Math.Max(MaxObservedConcurrency, active);
            try
            {
                await Task.Delay(_delay, cancellationToken);
                return DeliveryTargetDispatchResult.Success();
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }
}
