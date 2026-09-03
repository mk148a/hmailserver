using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class FileBackedImapSortIndexTests
{
    [TestMethod]
    public async Task SortAsync_WhenFullTextIsNotReady_FiltersMessageFilesAndPreservesSqlSortOrder()
    {
        var first = new MessageIdentity(1, 10, 20, 101);
        var second = new MessageIdentity(2, 10, 20, 102);
        var third = new MessageIdentity(3, 10, 20, 103);
        var inner = new FakeSortIndex([first, second, third]);
        var documents = new FakeDocumentSource(
            new Dictionary<long, MessageSearchDocument>
            {
                [1] = CreateDocument(first, subject: "invoice", body: "paid"),
                [2] = CreateDocument(second, subject: "notice", body: "invoice"),
                [3] = CreateDocument(third, subject: "invoice", body: "invoice")
            });
        var index = new FileBackedImapSortIndex(
            inner,
            documents,
            new FakeIndexingStore(enabled: true, totalMessageCount: 3, totalIndexedCount: 1, fullTextReady: false));

        var request = CreateRequest() with
        {
            SearchRequest = CreateRequest().SearchRequest with { AnyTerms = ["invoice"] }
        };
        var result = await CollectAsync(index.SortAsync(request, CancellationToken.None));

        CollectionAssert.AreEqual(new long[] { 101, 102, 103 }, result);
        Assert.IsNotNull(inner.LastRequest);
        CollectionAssert.AreEqual(Array.Empty<string>(), inner.LastRequest!.SearchRequest.GetAnyTerms().ToArray());
        Assert.AreEqual(3, documents.LoadCount);
    }

    [TestMethod]
    public async Task SortAsync_WhenFullTextIsNotReady_OmitsMissingFileDocuments()
    {
        var first = new MessageIdentity(1, 10, 20, 101);
        var second = new MessageIdentity(2, 10, 20, 102);
        var inner = new FakeSortIndex([first, second]);
        var documents = new FakeDocumentSource(
            new Dictionary<long, MessageSearchDocument>
            {
                [1] = CreateDocument(first, subject: "invoice", body: "paid")
            });
        var index = new FileBackedImapSortIndex(
            inner,
            documents,
            new FakeIndexingStore(enabled: false));

        var result = await CollectAsync(index.SortAsync(
            CreateRequest() with
            {
                SearchRequest = CreateRequest().SearchRequest with { SubjectTerms = ["invoice"] }
            },
            CancellationToken.None));

        CollectionAssert.AreEqual(new long[] { 101 }, result);
    }

    [TestMethod]
    public async Task SortAsync_WhenFullTextIsReady_UsesExistingSqlTextPath()
    {
        var identity = new MessageIdentity(1, 10, 20, 101);
        var inner = new FakeSortIndex([identity]);
        var documents = new FakeDocumentSource(new Dictionary<long, MessageSearchDocument>());
        var index = new FileBackedImapSortIndex(
            inner,
            documents,
            new FakeIndexingStore(enabled: true, totalMessageCount: 1, totalIndexedCount: 1, fullTextReady: true));

        var request = CreateRequest() with
        {
            SearchRequest = CreateRequest().SearchRequest with { AnyTerms = ["invoice"] }
        };
        var result = await CollectAsync(index.SortAsync(request, CancellationToken.None));

        CollectionAssert.AreEqual(new long[] { 101 }, result);
        Assert.IsNotNull(inner.LastRequest);
        CollectionAssert.AreEqual(new[] { "invoice" }, inner.LastRequest!.SearchRequest.GetAnyTerms().ToArray());
        Assert.AreEqual(0, documents.LoadCount);
    }

    private static ImapSortRequest CreateRequest() =>
        new(
            new ImapSearchRequest(
                AccountId: 10,
                FolderId: 20,
                MinUid: null,
                MaxUid: null,
                RequiredFlags: null,
                ForbiddenFlags: null,
                Since: null,
                Before: null,
                LargerThanBytes: null,
                SmallerThanBytes: null,
                HeaderText: null,
                BodyText: null,
                AnyText: null,
                ReturnUid: true),
            [new ImapSortCriterion(ImapSortKey.Date, Descending: false)]);

    private static MessageSearchDocument CreateDocument(
        MessageIdentity identity,
        string subject,
        string body) =>
        new(
            identity,
            DateTimeOffset.UtcNow,
            100,
            0,
            $"Subject: {subject}",
            body,
            $"Subject: {subject}\r\n\r\n{body}")
        {
            SubjectText = subject,
            FileSearchHeaderText = $"Subject: {subject}",
            FileSearchPlainBodyText = body,
            FileSearchHtmlBodyText = string.Empty
        };

    private static async Task<long[]> CollectAsync(IAsyncEnumerable<MessageIdentity> identities)
    {
        var result = new List<long>();
        await foreach (var identity in identities)
        {
            result.Add(identity.Uid);
        }

        return result.ToArray();
    }

    private sealed class FakeSortIndex : IMessageSortIndex
    {
        private readonly IReadOnlyList<MessageIdentity> _identities;

        public FakeSortIndex(IReadOnlyList<MessageIdentity> identities) => _identities = identities;

        public ImapSortRequest? LastRequest { get; private set; }

        public async IAsyncEnumerable<MessageIdentity> SortAsync(
            ImapSortRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastRequest = request;
            await Task.Yield();
            foreach (var identity in _identities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return identity;
            }
        }
    }

    private sealed class FakeDocumentSource : IMessageSearchDocumentSource
    {
        private readonly IReadOnlyDictionary<long, MessageSearchDocument> _documents;

        public FakeDocumentSource(IReadOnlyDictionary<long, MessageSearchDocument> documents) => _documents = documents;

        public int LoadCount { get; private set; }

        public ValueTask<MessageSearchDocument?> TryLoadAsync(
            MessageIdentity identity,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCount++;
            _documents.TryGetValue(identity.MessageId, out var document);
            return ValueTask.FromResult(document);
        }
    }

    private sealed class FakeIndexingStore : IMessageIndexingAdministrationStore
    {
        private readonly bool _enabled;
        private readonly MessageIndexingAdministrationStatus _status;

        public FakeIndexingStore(bool enabled, int totalMessageCount = 0, int totalIndexedCount = 0, bool fullTextReady = false)
        {
            _enabled = enabled;
            _status = new MessageIndexingAdministrationStatus(
                totalMessageCount,
                totalIndexedCount,
                enabled,
                fullTextReady,
                QueuedMessageCount: 0,
                LastError: string.Empty);
        }

        public ValueTask<bool> IsEnabledAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(_enabled);

        public ValueTask<MessageIndexingAdministrationStatus> GetStatusAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(_status);

        public ValueTask SetEnabledAsync(bool enabled, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask ClearAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask IndexAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask RebuildAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
