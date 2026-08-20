using HMailServer.Core.Abstractions;
using HMailServer.Delivery;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DeliveryQueueWorkerTests
{
    [TestMethod]
    public async Task RunAsync_DrainsFullBatchBeforeWaiting()
    {
        using var cancellation = new CancellationTokenSource();
        var processor = new ScriptedBatchProcessor(
            static _ => ValueTask.FromResult(2),
            static _ => ValueTask.FromResult(0));
        var signal = new RecordingWakeSignal((_, _, _) =>
        {
            cancellation.Cancel();
            return ValueTask.FromResult(false);
        });
        var worker = CreateWorker(processor, signal, new RecordingObserver(), batchSize: 2);

        await worker.RunAsync(cancellation.Token);

        Assert.AreEqual(2, processor.CallCount);
        Assert.AreEqual(1, signal.WaitCount);
        Assert.AreEqual(TimeSpan.FromMinutes(1), signal.Timeouts.Single());
    }

    [TestMethod]
    public async Task RunAsync_WaitsAfterPartialBatch()
    {
        using var cancellation = new CancellationTokenSource();
        var processor = new ScriptedBatchProcessor(
            static _ => ValueTask.FromResult(1));
        var signal = new RecordingWakeSignal((_, _, _) =>
        {
            cancellation.Cancel();
            return ValueTask.FromResult(false);
        });
        var worker = CreateWorker(processor, signal, new RecordingObserver(), batchSize: 2);

        await worker.RunAsync(cancellation.Token);

        Assert.AreEqual(1, processor.CallCount);
        Assert.AreEqual(1, signal.WaitCount);
        Assert.AreEqual(TimeSpan.FromMinutes(1), signal.Timeouts.Single());
    }

    [TestMethod]
    public async Task RunAsync_WakeSignalStartsAnotherBatch()
    {
        using var cancellation = new CancellationTokenSource();
        var processor = new ScriptedBatchProcessor(
            static _ => ValueTask.FromResult(0),
            static _ => ValueTask.FromResult(0));
        var signal = new RecordingWakeSignal((call, _, _) =>
        {
            if (call == 2)
            {
                cancellation.Cancel();
            }

            return ValueTask.FromResult(call == 1);
        });
        var worker = CreateWorker(processor, signal, new RecordingObserver(), batchSize: 2);

        await worker.RunAsync(cancellation.Token);

        Assert.AreEqual(2, processor.CallCount);
        Assert.AreEqual(2, signal.WaitCount);
    }

    [TestMethod]
    public async Task RunAsync_ReportsFailureWaitsAndRetries()
    {
        using var cancellation = new CancellationTokenSource();
        var expected = new InvalidOperationException("database unavailable");
        var processor = new ScriptedBatchProcessor(
            _ => ValueTask.FromException<int>(expected),
            static _ => ValueTask.FromResult(0));
        var signal = new RecordingWakeSignal((call, _, _) =>
        {
            if (call == 2)
            {
                cancellation.Cancel();
            }

            return ValueTask.FromResult(call == 1);
        });
        var observer = new RecordingObserver();
        var worker = CreateWorker(processor, signal, observer, batchSize: 2);

        await worker.RunAsync(cancellation.Token);

        Assert.AreEqual(2, processor.CallCount);
        Assert.AreSame(expected, observer.Exceptions.Single());
        CollectionAssert.AreEqual(
            new[] { TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1) },
            signal.Timeouts);
    }

    [TestMethod]
    public async Task RunAsync_PropagatesShutdownCancellationWithoutReportingFailure()
    {
        using var cancellation = new CancellationTokenSource();
        var processor = new ScriptedBatchProcessor(_ =>
        {
            cancellation.Cancel();
            return ValueTask.FromCanceled<int>(cancellation.Token);
        });
        var signal = new RecordingWakeSignal(static (_, _, _) => ValueTask.FromResult(false));
        var observer = new RecordingObserver();
        var worker = CreateWorker(processor, signal, observer, batchSize: 2);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => worker.RunAsync(cancellation.Token));

        Assert.AreEqual(1, processor.CallCount);
        Assert.AreEqual(0, signal.WaitCount);
        Assert.IsEmpty(observer.Exceptions);
    }

    [TestMethod]
    public async Task RunAsync_WaitsWhileClearOwnsPauseDrainGate()
    {
        using var cancellation = new CancellationTokenSource();
        using var gate = new DeliveryQueuePauseDrainGate();
        var processor = new ScriptedBatchProcessor(static _ => ValueTask.FromResult(0));
        var signal = new RecordingWakeSignal((_, _, _) =>
        {
            cancellation.Cancel();
            return ValueTask.FromResult(false);
        });
        var worker = CreateWorker(
            processor,
            signal,
            new RecordingObserver(),
            batchSize: 2,
            pauseDrainGate: gate);
        using var clearLease = await gate.PauseAndDrainAsync(CancellationToken.None);
        var workerTask = worker.RunAsync(cancellation.Token);

        await Task.Delay(50);
        Assert.AreEqual(0, processor.CallCount);

        clearLease.Dispose();
        await workerTask;

        Assert.AreEqual(1, processor.CallCount);
    }

    [TestMethod]
    public void Constructor_RejectsNonPositiveWaits()
    {
        var processor = new ScriptedBatchProcessor(static _ => ValueTask.FromResult(0));
        var signal = new RecordingWakeSignal(static (_, _, _) => ValueTask.FromResult(false));
        var observer = new RecordingObserver();
        var processorOptions = CreateProcessorOptions(batchSize: 2);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new DeliveryQueueWorker(
                new DeliveryQueueWorkerOptions(TimeSpan.Zero, TimeSpan.FromSeconds(1)),
                processorOptions,
                processor,
                signal,
                new DeliveryQueuePauseDrainGate(),
                observer));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new DeliveryQueueWorker(
                new DeliveryQueueWorkerOptions(TimeSpan.FromSeconds(1), TimeSpan.Zero),
                processorOptions,
                processor,
                signal,
                new DeliveryQueuePauseDrainGate(),
                observer));
    }

    private static DeliveryQueueWorker CreateWorker(
        IDeliveryQueueBatchProcessor processor,
        IDeliveryQueueWakeSignal signal,
        IDeliveryQueueWorkerObserver observer,
        int batchSize,
        DeliveryQueuePauseDrainGate? pauseDrainGate = null) =>
        new(
            DeliveryQueueWorkerOptions.Default,
            CreateProcessorOptions(batchSize),
            processor,
            signal,
            pauseDrainGate ?? new DeliveryQueuePauseDrainGate(),
            observer);

    private static DeliveryQueueProcessorOptions CreateProcessorOptions(int batchSize) =>
        new(
            LeaseOwner: "worker-test",
            BatchSize: batchSize,
            LeaseDuration: TimeSpan.FromMinutes(5),
            RetryDelay: TimeSpan.FromMinutes(5),
            MaxRetries: 4,
            MaxRetryDelay: TimeSpan.FromHours(4));

    private sealed class ScriptedBatchProcessor : IDeliveryQueueBatchProcessor
    {
        private readonly Queue<Func<CancellationToken, ValueTask<int>>> _responses;

        public ScriptedBatchProcessor(params Func<CancellationToken, ValueTask<int>>[] responses)
        {
            _responses = new Queue<Func<CancellationToken, ValueTask<int>>>(responses);
        }

        public int CallCount { get; private set; }

        public ValueTask<int> RunBatchAsync(
            DeliveryQueueProcessorOptions options,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return _responses.Dequeue()(cancellationToken);
        }
    }

    private sealed class RecordingWakeSignal : IDeliveryQueueWakeSignal
    {
        private readonly Func<int, TimeSpan, CancellationToken, ValueTask<bool>> _wait;

        public RecordingWakeSignal(
            Func<int, TimeSpan, CancellationToken, ValueTask<bool>> wait)
        {
            _wait = wait;
        }

        public int WaitCount { get; private set; }

        public List<TimeSpan> Timeouts { get; } = [];

        public void Signal()
        {
        }

        public ValueTask<bool> WaitAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            WaitCount++;
            Timeouts.Add(timeout);
            return _wait(WaitCount, timeout, cancellationToken);
        }
    }

    private sealed class RecordingObserver : IDeliveryQueueWorkerObserver
    {
        public List<Exception> Exceptions { get; } = [];

        public void ProcessingFailed(Exception exception) => Exceptions.Add(exception);
    }
}
