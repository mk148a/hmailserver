using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SignalingSmtpQueueWriterTests
{
    [TestMethod]
    public async Task EnqueueAsync_SignalsOnlyAfterDurableWriterCompletes()
    {
        var calls = new List<string>();
        var inner = new StubQueueWriter((_, _) =>
        {
            calls.Add("enqueue");
            return ValueTask.CompletedTask;
        });
        var signal = new StubWakeSignal(() => calls.Add("signal"));
        var writer = new SignalingSmtpQueueWriter(inner, signal);

        await writer.EnqueueAsync(CreateRequest(), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "enqueue", "signal" }, calls);
    }

    [TestMethod]
    public async Task EnqueueAsync_DoesNotSignalWhenDurableWriterFails()
    {
        var expected = new IOException("transaction rolled back");
        var signal = new StubWakeSignal();
        var writer = new SignalingSmtpQueueWriter(
            new StubQueueWriter((_, _) => ValueTask.FromException(expected)),
            signal);

        var actual = await Assert.ThrowsExactlyAsync<IOException>(
            () => writer.EnqueueAsync(CreateRequest(), CancellationToken.None).AsTask());

        Assert.AreSame(expected, actual);
        Assert.AreEqual(0, signal.SignalCount);
    }

    [TestMethod]
    public async Task EnqueueAsync_DoesNotSignalWhenDurableWriterIsCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var signal = new StubWakeSignal();
        var writer = new SignalingSmtpQueueWriter(
            new StubQueueWriter((_, token) => ValueTask.FromCanceled(token)),
            signal);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => writer.EnqueueAsync(CreateRequest(), cancellation.Token).AsTask());

        Assert.AreEqual(0, signal.SignalCount);
    }

    [TestMethod]
    public async Task EnqueueAsync_DoesNotFailDurableMessageWhenSignalThrows()
    {
        var inner = new StubQueueWriter(static (_, _) => ValueTask.CompletedTask);
        var signal = new StubWakeSignal(
            static () => throw new ObjectDisposedException("delivery wake signal"));
        var writer = new SignalingSmtpQueueWriter(inner, signal);

        await writer.EnqueueAsync(CreateRequest(), CancellationToken.None);

        Assert.AreEqual(1, signal.SignalCount);
    }

    private static SmtpQueueWriteRequest CreateRequest() =>
        new(
            "sender@example.test",
            [
                new SmtpResolvedRecipient(
                    "recipient@example.test",
                    "recipient@example.test",
                    LocalAccountId: 0,
                    IsLocal: false)
            ],
            "Subject: Wake\r\n\r\nBody\r\n"u8.ToArray(),
            DateTimeOffset.UtcNow);

    private sealed class StubQueueWriter : ISmtpQueueWriter
    {
        private readonly Func<SmtpQueueWriteRequest, CancellationToken, ValueTask> _enqueue;

        public StubQueueWriter(
            Func<SmtpQueueWriteRequest, CancellationToken, ValueTask> enqueue)
        {
            _enqueue = enqueue;
        }

        public ValueTask EnqueueAsync(
            SmtpQueueWriteRequest request,
            CancellationToken cancellationToken) =>
            _enqueue(request, cancellationToken);
    }

    private sealed class StubWakeSignal : IDeliveryQueueWakeSignal
    {
        private readonly Action _signal;

        public StubWakeSignal(Action? signal = null)
        {
            _signal = signal ?? (() => { });
        }

        public int SignalCount { get; private set; }

        public void Signal()
        {
            SignalCount++;
            _signal();
        }

        public ValueTask<bool> WaitAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
