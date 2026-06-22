using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using HMailServer.Indexing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
