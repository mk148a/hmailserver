using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using HMailServer.Indexing;
using HMailServer.Service;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class MessageSearchBackfillProcessorTests
{
    private static readonly MessageIdentity Identity = new(100, 10, 20, 30);

    [TestMethod]
    public async Task RunBatchAsync_UpsertsDocumentAndMarksSuccess()
    {
        var document = CreateDocument();
        var store = new FakeBackfillStore([Identity]);
        var source = new FakeDocumentSource(document);
        var index = new FakeSearchIndex();
        var processor = new MessageSearchBackfillProcessor(store, source, index);

        var processed = await processor.RunBatchAsync(
            MessageSearchBackfillOptions.Default("worker-1"),
            CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(1, index.Upserted.Count);
        Assert.AreEqual(Identity, store.Succeeded.Single());
        Assert.AreEqual(0, store.Failed.Count);
    }

    [TestMethod]
    public async Task RunBatchAsync_MarksFailureWhenSourceReturnsNoDocument()
    {
        var store = new FakeBackfillStore([Identity]);
        var source = new FakeDocumentSource(null);
        var index = new FakeSearchIndex();
        var processor = new MessageSearchBackfillProcessor(store, source, index);

        var processed = await processor.RunBatchAsync(
            MessageSearchBackfillOptions.Default("worker-1"),
            CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(0, index.Upserted.Count);
        Assert.AreEqual(0, store.Succeeded.Count);
        Assert.AreEqual(Identity, store.Failed.Single().Identity);
    }

    [TestMethod]
    public async Task RunBatchAsync_MarksFailureWhenUpsertFails()
    {
        var store = new FakeBackfillStore([Identity]);
        var source = new FakeDocumentSource(CreateDocument());
        var index = new FakeSearchIndex(new InvalidOperationException("boom"));
        var processor = new MessageSearchBackfillProcessor(store, source, index);

        var processed = await processor.RunBatchAsync(
            MessageSearchBackfillOptions.Default("worker-1"),
            CancellationToken.None);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(0, store.Succeeded.Count);
        Assert.AreEqual("boom", store.Failed.Single().Error);
    }

    [TestMethod]
    public async Task RunBatchAsync_DoesNotLeaseMessagesWhenIndexingIsDisabled()
    {
        var store = new FakeBackfillStore([Identity]);
        var source = new FakeDocumentSource(CreateDocument());
        var index = new FakeSearchIndex();
        var administrationStore = new FakeAdministrationStore(enabled: false);
        var processor = new MessageSearchBackfillProcessor(
            store,
            source,
            index,
            administrationStore);

        var processed = await processor.RunBatchAsync(
            MessageSearchBackfillOptions.Default("worker-1"),
            CancellationToken.None);

        Assert.AreEqual(0, processed);
        Assert.AreEqual(0, store.LeaseCalls);
        Assert.AreEqual(0, index.Upserted.Count);
    }

    [TestMethod]
    public async Task HostedService_UsesExponentialRetryDelayForTransientBatchFailures()
    {
        var administrationStore = new ThrowingAdministrationStore();
        var processor = new MessageSearchBackfillProcessor(
            new FakeBackfillStore([]),
            new FakeDocumentSource(null),
            new FakeSearchIndex(),
            administrationStore);
        var readiness = new ServerReadinessSignal();
        readiness.SetBootstrapComplete();
        var logger = new CapturingLogger<MessageSearchBackfillHostedService>();
        var service = new MessageSearchBackfillHostedService(
            MessageSearchBackfillOptions.Default("worker-1"),
            processor,
            logger,
            readiness);
        using var cancellation = new CancellationTokenSource();

        await service.StartAsync(cancellation.Token);
        await administrationStore.FirstCall.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await administrationStore.SecondCall.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await administrationStore.ResetRetryCall.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await logger.ThreeRetryDelaysLogged.Task.WaitAsync(TimeSpan.FromSeconds(2));

        CollectionAssert.AreEqual(
            new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(2) },
            logger.RetryDelays.ToArray());
        Assert.IsFalse(service.ExecuteTask?.IsFaulted ?? true);

        cancellation.Cancel();
        await service.StopAsync(CancellationToken.None);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly object _sync = new();
        private readonly List<TimeSpan> _retryDelays = [];

        public TaskCompletionSource<bool> ThreeRetryDelaysLogged { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<TimeSpan> RetryDelays
        {
            get
            {
                lock (_sync)
                {
                    return [.. _retryDelays];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> values)
            {
                return;
            }

            foreach (var pair in values)
            {
                if (pair.Key != "RetryDelay" || pair.Value is not TimeSpan retryDelay)
                {
                    continue;
                }

                lock (_sync)
                {
                    _retryDelays.Add(retryDelay);
                    if (_retryDelays.Count == 3)
                    {
                        ThreeRetryDelaysLogged.TrySetResult(true);
                    }
                }

                return;
            }
        }
    }

    private static MessageSearchDocument CreateDocument()
    {
        return new MessageSearchDocument(
            Identity,
            DateTimeOffset.UtcNow,
            SizeBytes: 1234,
            Flags: 1,
            HeaderText: "Subject: Test",
            BodyText: "Body",
            CombinedText: "Subject: Test Body");
    }

    private sealed class FakeBackfillStore : IMessageSearchBackfillStore
    {
        private readonly IReadOnlyList<MessageIdentity> _leased;

        public FakeBackfillStore(IReadOnlyList<MessageIdentity> leased)
        {
            _leased = leased;
        }

        public List<MessageIdentity> Succeeded { get; } = [];

        public List<(MessageIdentity Identity, string Error)> Failed { get; } = [];

        public int LeaseCalls { get; private set; }

        public async IAsyncEnumerable<MessageIdentity> LeaseBatchAsync(
            string leaseOwner,
            int batchSize,
            TimeSpan leaseDuration,
            int maxAttempts,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LeaseCalls++;
            foreach (var identity in _leased)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return identity;
                await Task.Yield();
            }
        }

        public ValueTask MarkSucceededAsync(
            MessageIdentity identity,
            string leaseOwner,
            CancellationToken cancellationToken)
        {
            Succeeded.Add(identity);
            return ValueTask.CompletedTask;
        }

        public ValueTask MarkFailedAsync(
            MessageIdentity identity,
            string leaseOwner,
            string error,
            TimeSpan retryDelay,
            CancellationToken cancellationToken)
        {
            Failed.Add((identity, error));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeAdministrationStore : IMessageIndexingAdministrationStore
    {
        private readonly bool _enabled;

        public FakeAdministrationStore(bool enabled)
        {
            _enabled = enabled;
        }

        public ValueTask<bool> IsEnabledAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(_enabled);

        public ValueTask<MessageIndexingAdministrationStatus> GetStatusAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask SetEnabledAsync(bool enabled, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask ClearAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask IndexAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask RebuildAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingAdministrationStore : IMessageIndexingAdministrationStore
    {
        private int _calls;

        public TaskCompletionSource<bool> FirstCall { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> SecondCall { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ResetRetryCall { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<bool> IsEnabledAsync(CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _calls);
            if (call == 1)
            {
                FirstCall.TrySetResult(true);
            }
            else if (call == 2)
            {
                SecondCall.TrySetResult(true);
            }
            else if (call == 3)
            {
                return ValueTask.FromResult(true);
            }
            else if (call == 4)
            {
                ResetRetryCall.TrySetResult(true);
            }

            throw new TimeoutException("Synthetic SQL connection-pool timeout.");
        }

        public ValueTask<MessageIndexingAdministrationStatus> GetStatusAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask SetEnabledAsync(bool enabled, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask ClearAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask IndexAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask RebuildAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeDocumentSource : IMessageSearchDocumentSource
    {
        private readonly MessageSearchDocument? _document;

        public FakeDocumentSource(MessageSearchDocument? document)
        {
            _document = document;
        }

        public ValueTask<MessageSearchDocument?> TryLoadAsync(
            MessageIdentity identity,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(_document);
        }
    }

    private sealed class FakeSearchIndex : IMessageSearchIndex
    {
        private readonly Exception? _upsertException;

        public FakeSearchIndex(Exception? upsertException = null)
        {
            _upsertException = upsertException;
        }

        public List<MessageSearchDocument> Upserted { get; } = [];

        public ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(true);
        }

        public ValueTask QueueForIndexingAsync(
            MessageIdentity identity,
            CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask UpsertAsync(
            MessageSearchDocument document,
            CancellationToken cancellationToken)
        {
            if (_upsertException is not null)
            {
                throw _upsertException;
            }

            Upserted.Add(document);
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<MessageIdentity> SearchAsync(
            ImapSearchRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
