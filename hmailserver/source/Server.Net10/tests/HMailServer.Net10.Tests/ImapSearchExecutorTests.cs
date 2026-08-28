using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapSearchExecutorTests
{
    [TestMethod]
    public async Task ExecuteAsync_ReturnsUidSearchResponse()
    {
        var request = CreateRequest(returnUid: true);
        var executor = new ImapSearchExecutor(
            new FakeMessageSearchIndex(
            [
                new MessageIdentity(1, 10, 20, 101),
                new MessageIdentity(2, 10, 20, 105)
            ]));

        var response = await executor.ExecuteAsync(request, CancellationToken.None);

        Assert.AreEqual("* SEARCH 101 105\r\n", response);
    }

    [TestMethod]
    public async Task ExecuteAsync_UsesSequenceResolverForNonUidSearch()
    {
        var request = CreateRequest(returnUid: false);
        var executor = new ImapSearchExecutor(
            new FakeMessageSearchIndex(
            [
                new MessageIdentity(1, 10, 20, 101),
                new MessageIdentity(2, 10, 20, 105)
            ]),
            new FakeSequenceNumberResolver());

        var response = await executor.ExecuteAsync(request, CancellationToken.None);

        Assert.AreEqual("* SEARCH 7 9\r\n", response);
    }

    [TestMethod]
    public async Task ExecuteAsync_ThrowsForNonUidSearchWithoutSequenceResolver()
    {
        var executor = new ImapSearchExecutor(
            new FakeMessageSearchIndex(
            [
                new MessageIdentity(1, 10, 20, 101)
            ]));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(CreateRequest(returnUid: false), CancellationToken.None).AsTask());
    }

    [TestMethod]
    public async Task ExecuteAsync_SubjectReturnsPositiveSubstringMatch()
    {
        var response = await ExecuteSubjectSearchAsync("report", "Quarterly report ready");

        Assert.AreEqual("* SEARCH 101\r\n", response);
    }

    [TestMethod]
    public async Task ExecuteAsync_SubjectRejectsNonMatch()
    {
        var response = await ExecuteSubjectSearchAsync("invoice", "Quarterly report ready");

        Assert.AreEqual("* SEARCH\r\n", response);
    }

    [TestMethod]
    public async Task ExecuteAsync_SubjectMatchesCaseInsensitiveSubstring()
    {
        var response = await ExecuteSubjectSearchAsync("REPORT", "Quarterly report ready");

        Assert.AreEqual("* SEARCH 101\r\n", response);
    }

    [TestMethod]
    public async Task ExecuteAsync_SubjectRejectsNeedleOnlyInOtherHeader()
    {
        var response = await ExecuteSubjectSearchAsync(
            "needle",
            subject: "Quarterly report ready",
            headerText: "X-Tracking: needle");

        Assert.AreEqual("* SEARCH\r\n", response);
    }

    [TestMethod]
    public async Task ExecuteAsync_TextWithIndexingDisabled_RemovesAnyTermsFromCandidateRequest()
    {
        var identity = new MessageIdentity(1, 10, 20, 101);
        var index = new FakeMessageSearchIndex([identity]);
        var executor = new ImapSearchExecutor(
            index,
            documentSource: new FakeDocumentSource(CreateDocument(identity, plainBodyText: "invoice paid")),
            indexingAdministrationStore: new FakeAdministrationStore(enabled: false));

        var response = await executor.ExecuteAsync(
            CreateRequest(returnUid: true) with { AnyTerms = ["paid"] },
            CancellationToken.None);

        Assert.AreEqual("* SEARCH 101\r\n", response);
        Assert.IsNotNull(index.LastRequest);
        Assert.IsNull(index.LastRequest.AnyText);
        Assert.AreEqual(0, index.LastRequest.GetAnyTerms().Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_TextWithIndexingPartiallyCovered_RemovesTextPredicatesAndFiltersFiles()
    {
        var firstIdentity = new MessageIdentity(1, 10, 20, 101);
        var secondIdentity = new MessageIdentity(2, 10, 20, 102);
        var index = new FakeMessageSearchIndex([firstIdentity, secondIdentity]);
        var documentSource = new FakeBatchDocumentSource(
            CreateDocument(firstIdentity, headerText: "X-Tracking: invoice", plainBodyText: "paid"),
            CreateDocument(secondIdentity, headerText: "X-Tracking: invoice", plainBodyText: "pending"));
        var request = CreateRequest(returnUid: true) with
        {
            MinUid = 100,
            MaxUid = 200,
            RequiredFlags = 1,
            Since = new DateOnly(2026, 1, 1),
            HeaderText = "tracking",
            HeaderTerms = ["invoice"],
            BodyText = "paid",
            BodyTerms = ["paid"],
            AnyText = "invoice",
            AnyTerms = ["invoice"]
        };
        var executor = new ImapSearchExecutor(
            index,
            documentSource: documentSource,
            indexingAdministrationStore: new FakeAdministrationStore(
                enabled: true,
                totalMessageCount: 2,
                totalIndexedCount: 1));

        var response = await executor.ExecuteAsync(request, CancellationToken.None);

        Assert.AreEqual("* SEARCH 101\r\n", response);
        Assert.IsNotNull(index.LastRequest);
        Assert.IsNull(index.LastRequest.HeaderText);
        Assert.IsNull(index.LastRequest.BodyText);
        Assert.IsNull(index.LastRequest.AnyText);
        Assert.AreEqual(0, index.LastRequest.GetHeaderTerms().Count);
        Assert.AreEqual(0, index.LastRequest.GetBodyTerms().Count);
        Assert.AreEqual(0, index.LastRequest.GetAnyTerms().Count);
        Assert.AreEqual(100L, index.LastRequest.MinUid);
        Assert.AreEqual(200L, index.LastRequest.MaxUid);
        Assert.AreEqual((byte)1, index.LastRequest.RequiredFlags);
        Assert.AreEqual(new DateOnly(2026, 1, 1), index.LastRequest.Since);
    }

    [TestMethod]
    public async Task ExecuteAsync_TextWithIndexingDisabled_MatchesDecodedHeader()
    {
        var response = await ExecuteTextFallbackAsync(
            "résumé",
            CreateDocument(headerText: "X-Description: Quarterly résumé"));

        Assert.AreEqual("* SEARCH 101\r\n", response);
    }

    [TestMethod]
    public async Task ExecuteAsync_TextWithIndexingDisabled_MatchesPlainBody()
    {
        var response = await ExecuteTextFallbackAsync(
            "invoice",
            CreateDocument(plainBodyText: "The invoice is ready."));

        Assert.AreEqual("* SEARCH 101\r\n", response);
    }

    [TestMethod]
    public async Task ExecuteAsync_TextWithIndexingDisabled_MatchesRawHtmlBody()
    {
        var response = await ExecuteTextFallbackAsync(
            "data-marker=\"needle\"",
            CreateDocument(htmlBodyText: "<p data-marker=\"needle\">Report</p>"));

        Assert.AreEqual("* SEARCH 101\r\n", response);
    }

    [TestMethod]
    public async Task ExecuteAsync_TextWithIndexingDisabled_MatchesCaseInsensitiveMiddleSubstring()
    {
        var response = await ExecuteTextFallbackAsync(
            "MIDDLE",
            CreateDocument(plainBodyText: "prefix-middle-suffix"));

        Assert.AreEqual("* SEARCH 101\r\n", response);
    }

    [TestMethod]
    public async Task ExecuteAsync_TextWithIndexingDisabled_DoesNotMatchAcrossDomains()
    {
        var response = await ExecuteTextFallbackAsync(
            "endstart",
            CreateDocument(headerText: "X-Value: end", plainBodyText: "start of body"));

        Assert.AreEqual("* SEARCH\r\n", response);
    }

    [TestMethod]
    public async Task ExecuteAsync_TextWithIndexingDisabled_MissingDocumentDoesNotStopLaterMatches()
    {
        var missingIdentity = new MessageIdentity(1, 10, 20, 101);
        var matchingIdentity = new MessageIdentity(2, 10, 20, 105);
        var documentSource = new FakeDocumentSource(
            CreateDocument(matchingIdentity, plainBodyText: "invoice"));
        var executor = new ImapSearchExecutor(
            new FakeMessageSearchIndex([missingIdentity, matchingIdentity]),
            documentSource: documentSource,
            indexingAdministrationStore: new FakeAdministrationStore(enabled: false));

        var response = await executor.ExecuteAsync(CreateRequest(returnUid: true), CancellationToken.None);

        Assert.AreEqual("* SEARCH 105\r\n", response);
    }

    [TestMethod]
    public async Task ExecuteAsync_TextWithIndexingDisabled_PreservesCandidateOrdering()
    {
        var firstIdentity = new MessageIdentity(1, 10, 20, 101);
        var secondIdentity = new MessageIdentity(2, 10, 20, 105);
        var executor = new ImapSearchExecutor(
            new FakeMessageSearchIndex([firstIdentity, secondIdentity]),
            documentSource: new FakeDocumentSource(
                CreateDocument(firstIdentity, plainBodyText: "invoice one"),
                CreateDocument(secondIdentity, plainBodyText: "invoice two")),
            indexingAdministrationStore: new FakeAdministrationStore(enabled: false));

        var response = await executor.ExecuteAsync(CreateRequest(returnUid: true), CancellationToken.None);

        Assert.AreEqual("* SEARCH 101 105\r\n", response);
    }

    [TestMethod]
    public async Task ExecuteAsync_TextWithIndexingDisabled_UsesBatchSourceIn128ItemBatches()
    {
        var identities = Enumerable.Range(1, 130)
            .Select(value => new MessageIdentity(value, 10, 20, 100 + value))
            .ToArray();
        var documentSource = new FakeBatchDocumentSource(
            identities.Select(identity => CreateDocument(identity, plainBodyText: "invoice")).ToArray());
        var executor = new ImapSearchExecutor(
            new FakeMessageSearchIndex(identities),
            documentSource: documentSource,
            indexingAdministrationStore: new FakeAdministrationStore(enabled: false));

        var response = await executor.ExecuteAsync(CreateRequest(returnUid: true), CancellationToken.None);

        Assert.AreEqual($"* SEARCH {string.Join(' ', identities.Select(identity => identity.Uid))}\r\n", response);
        Assert.AreEqual(2, documentSource.Batches.Count);
        Assert.AreEqual(128, documentSource.Batches[0].Count);
        Assert.AreEqual(2, documentSource.Batches[1].Count);
        CollectionAssert.AreEqual(identities, documentSource.Batches.SelectMany(batch => batch).ToArray());
        Assert.AreEqual(0, documentSource.SingleLoadCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_TextWithIndexingDisabled_PreservesPositionalNulls()
    {
        var identities = new[]
        {
            new MessageIdentity(1, 10, 20, 101),
            new MessageIdentity(2, 10, 20, 102),
            new MessageIdentity(3, 10, 20, 103)
        };
        var documentSource = new FakeBatchDocumentSource(
            CreateDocument(identities[0], plainBodyText: "invoice"),
            CreateDocument(identities[2], plainBodyText: "invoice"));
        var executor = new ImapSearchExecutor(
            new FakeMessageSearchIndex(identities),
            documentSource: documentSource,
            indexingAdministrationStore: new FakeAdministrationStore(enabled: false));

        var response = await executor.ExecuteAsync(CreateRequest(returnUid: true), CancellationToken.None);

        Assert.AreEqual("* SEARCH 101 103\r\n", response);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(4)]
    public async Task ExecuteAsync_TextWithIndexingDisabled_RejectsInvalidBatchOutputCount(int outputCount)
    {
        var identities = new[]
        {
            new MessageIdentity(1, 10, 20, 101),
            new MessageIdentity(2, 10, 20, 102),
            new MessageIdentity(3, 10, 20, 103)
        };
        var executor = new ImapSearchExecutor(
            new FakeMessageSearchIndex(identities),
            documentSource: new InvalidCountBatchDocumentSource(outputCount),
            indexingAdministrationStore: new FakeAdministrationStore(enabled: false));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(CreateRequest(returnUid: true), CancellationToken.None).AsTask());
    }

    [TestMethod]
    public async Task ExecuteAsync_TextWithIndexingDisabled_ObservesCancellationDuringBatchLoad()
    {
        var identity = new MessageIdentity(1, 10, 20, 101);
        using var cancellation = new CancellationTokenSource();
        var executor = new ImapSearchExecutor(
            new FakeMessageSearchIndex([identity]),
            documentSource: new CancelingBatchDocumentSource(
                CreateDocument(identity, plainBodyText: "invoice"),
                cancellation),
            indexingAdministrationStore: new FakeAdministrationStore(enabled: false));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => executor.ExecuteAsync(CreateRequest(returnUid: true), cancellation.Token).AsTask());
    }

    [TestMethod]
    public async Task ExecuteAsync_TextWithIndexingDisabled_FallsBackToPerItemSource()
    {
        var firstIdentity = new MessageIdentity(1, 10, 20, 101);
        var secondIdentity = new MessageIdentity(2, 10, 20, 102);
        var documentSource = new FakeDocumentSource(
            CreateDocument(firstIdentity, plainBodyText: "invoice"),
            CreateDocument(secondIdentity, plainBodyText: "invoice"));
        var executor = new ImapSearchExecutor(
            new FakeMessageSearchIndex([firstIdentity, secondIdentity]),
            documentSource: documentSource,
            indexingAdministrationStore: new FakeAdministrationStore(enabled: false));

        var response = await executor.ExecuteAsync(CreateRequest(returnUid: true), CancellationToken.None);

        Assert.AreEqual("* SEARCH 101 102\r\n", response);
        Assert.AreEqual(2, documentSource.LoadCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_TextWithIndexingDisabled_ObservesCancellationAfterDocumentLoad()
    {
        var identity = new MessageIdentity(1, 10, 20, 101);
        using var cancellation = new CancellationTokenSource();
        var executor = new ImapSearchExecutor(
            new FakeMessageSearchIndex([identity]),
            documentSource: new CancelingDocumentSource(
                CreateDocument(identity, plainBodyText: "invoice"),
                cancellation),
            indexingAdministrationStore: new FakeAdministrationStore(enabled: false));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => executor.ExecuteAsync(CreateRequest(returnUid: true), cancellation.Token).AsTask());
    }

    [TestMethod]
    public async Task ExecuteAsync_TextWithIndexingEnabled_RetainsAnyTermsInSearchRequest()
    {
        var identity = new MessageIdentity(1, 10, 20, 101);
        var index = new FakeMessageSearchIndex([identity]);
        var request = CreateRequest(returnUid: true) with { AnyTerms = ["paid"] };
        var executor = new ImapSearchExecutor(
            index,
            indexingAdministrationStore: new FakeAdministrationStore(enabled: true));

        var response = await executor.ExecuteAsync(request, CancellationToken.None);

        Assert.AreEqual("* SEARCH 101\r\n", response);
        Assert.AreSame(request, index.LastRequest);
        CollectionAssert.AreEqual(new[] { "invoice", "paid" }, index.LastRequest!.GetAnyTerms().ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_TextWithIndexingEnabled_DoesNotLoadDocuments()
    {
        var identity = new MessageIdentity(1, 10, 20, 101);
        var documentSource = new FakeBatchDocumentSource(CreateDocument(identity, plainBodyText: "invoice"));
        var executor = new ImapSearchExecutor(
            new FakeMessageSearchIndex([identity]),
            documentSource: documentSource,
            indexingAdministrationStore: new FakeAdministrationStore(enabled: true));

        var response = await executor.ExecuteAsync(CreateRequest(returnUid: true), CancellationToken.None);

        Assert.AreEqual("* SEARCH 101\r\n", response);
        Assert.AreEqual(0, documentSource.Batches.Count);
        Assert.AreEqual(0, documentSource.SingleLoadCount);
    }

    [TestMethod]
    public void Format_ReturnsEmptySearchWhenNoIdentifiersMatch()
    {
        Assert.AreEqual("* SEARCH\r\n", ImapSearchResultFormatter.Format(Array.Empty<long>()));
    }

    private static ImapSearchRequest CreateRequest(bool returnUid) =>
        new(
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
            AnyText: "invoice",
            ReturnUid: returnUid);

    private static async Task<string> ExecuteSubjectSearchAsync(
        string term,
        string subject,
        string? headerText = null)
    {
        var identity = new MessageIdentity(1, 10, 20, 101);
        var request = CreateRequest(returnUid: true) with
        {
            AnyText = null,
            SubjectTerms = [term]
        };
        var document = new MessageSearchDocument(
            identity,
            DateTimeOffset.UtcNow,
            SizeBytes: 100,
            Flags: 0,
            HeaderText: headerText ?? $"Subject: {subject}",
            BodyText: string.Empty,
            CombinedText: headerText ?? $"Subject: {subject}")
        {
            SubjectText = subject
        };
        var executor = new ImapSearchExecutor(
            new FakeMessageSearchIndex([identity]),
            documentSource: new FakeDocumentSource(document));

        return await executor.ExecuteAsync(request, CancellationToken.None);
    }

    private static async Task<string> ExecuteTextFallbackAsync(
        string term,
        MessageSearchDocument document)
    {
        var request = CreateRequest(returnUid: true) with
        {
            AnyText = null,
            AnyTerms = [term]
        };
        var executor = new ImapSearchExecutor(
            new FakeMessageSearchIndex([document.Identity]),
            documentSource: new FakeDocumentSource(document),
            indexingAdministrationStore: new FakeAdministrationStore(enabled: false));

        return await executor.ExecuteAsync(request, CancellationToken.None);
    }

    private static MessageSearchDocument CreateDocument(
        MessageIdentity? identity = null,
        string headerText = "",
        string plainBodyText = "",
        string htmlBodyText = "")
    {
        var documentIdentity = identity ?? new MessageIdentity(1, 10, 20, 101);
        return new MessageSearchDocument(
            documentIdentity,
            DateTimeOffset.UtcNow,
            SizeBytes: 100,
            Flags: 0,
            HeaderText: string.Empty,
            BodyText: string.Empty,
            CombinedText: string.Empty)
        {
            FileSearchHeaderText = headerText,
            FileSearchPlainBodyText = plainBodyText,
            FileSearchHtmlBodyText = htmlBodyText
        };
    }

    private sealed class FakeMessageSearchIndex : IMessageSearchIndex
    {
        private readonly IReadOnlyList<MessageIdentity> _identities;

        public ImapSearchRequest? LastRequest { get; private set; }

        public FakeMessageSearchIndex(IReadOnlyList<MessageIdentity> identities)
        {
            _identities = identities;
        }

        public ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);

        public ValueTask QueueForIndexingAsync(MessageIdentity identity, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask UpsertAsync(MessageSearchDocument document, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public async IAsyncEnumerable<MessageIdentity> SearchAsync(
            ImapSearchRequest request,
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

    private sealed class FakeSequenceNumberResolver : IImapSequenceNumberResolver
    {
        public ValueTask<IReadOnlyDictionary<long, long>> ResolveMailboxSequenceNumbersAsync(
            int accountId,
            int folderId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyDictionary<long, long>>(
                new Dictionary<long, long>
                {
                    [1] = 7,
                    [2] = 9
                });
    }

    private sealed class FakeDocumentSource : IMessageSearchDocumentSource
    {
        private readonly IReadOnlyDictionary<long, MessageSearchDocument> _documents;

        public FakeDocumentSource(params MessageSearchDocument[] documents)
        {
            _documents = documents.ToDictionary(document => document.Identity.MessageId);
        }

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

    private sealed class FakeBatchDocumentSource : IMessageSearchDocumentBatchSource
    {
        private readonly IReadOnlyDictionary<long, MessageSearchDocument> _documents;

        public FakeBatchDocumentSource(params MessageSearchDocument[] documents)
        {
            _documents = documents.ToDictionary(document => document.Identity.MessageId);
        }

        public List<IReadOnlyList<MessageIdentity>> Batches { get; } = [];

        public int SingleLoadCount { get; private set; }

        public ValueTask<MessageSearchDocument?> TryLoadAsync(
            MessageIdentity identity,
            CancellationToken cancellationToken)
        {
            SingleLoadCount++;
            throw new AssertFailedException("The executor should use the batch source.");
        }

        public async IAsyncEnumerable<MessageSearchDocument?> TryLoadBatchAsync(
            IReadOnlyList<MessageIdentity> identities,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Batches.Add(identities.ToArray());
            await Task.Yield();

            foreach (var identity in identities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _documents.TryGetValue(identity.MessageId, out var document);
                yield return document;
            }
        }
    }

    private sealed class InvalidCountBatchDocumentSource : IMessageSearchDocumentBatchSource
    {
        private readonly int _outputCount;

        public InvalidCountBatchDocumentSource(int outputCount)
        {
            _outputCount = outputCount;
        }

        public ValueTask<MessageSearchDocument?> TryLoadAsync(
            MessageIdentity identity,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException("The executor should use the batch source.");

        public async IAsyncEnumerable<MessageSearchDocument?> TryLoadBatchAsync(
            IReadOnlyList<MessageIdentity> identities,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();

            for (var index = 0; index < _outputCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var identity = identities[Math.Min(index, identities.Count - 1)];
                yield return CreateDocument(identity, plainBodyText: "invoice");
            }
        }
    }

    private sealed class CancelingBatchDocumentSource : IMessageSearchDocumentBatchSource
    {
        private readonly MessageSearchDocument _document;
        private readonly CancellationTokenSource _cancellation;

        public CancelingBatchDocumentSource(
            MessageSearchDocument document,
            CancellationTokenSource cancellation)
        {
            _document = document;
            _cancellation = cancellation;
        }

        public ValueTask<MessageSearchDocument?> TryLoadAsync(
            MessageIdentity identity,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException("The executor should use the batch source.");

        public async IAsyncEnumerable<MessageSearchDocument?> TryLoadBatchAsync(
            IReadOnlyList<MessageIdentity> identities,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            _cancellation.Cancel();
            yield return _document;
        }
    }

    private sealed class CancelingDocumentSource : IMessageSearchDocumentSource
    {
        private readonly MessageSearchDocument _document;
        private readonly CancellationTokenSource _cancellation;

        public CancelingDocumentSource(
            MessageSearchDocument document,
            CancellationTokenSource cancellation)
        {
            _document = document;
            _cancellation = cancellation;
        }

        public ValueTask<MessageSearchDocument?> TryLoadAsync(
            MessageIdentity identity,
            CancellationToken cancellationToken)
        {
            _cancellation.Cancel();
            return ValueTask.FromResult<MessageSearchDocument?>(_document);
        }
    }

    private sealed class FakeAdministrationStore : IMessageIndexingAdministrationStore
    {
        private readonly bool _enabled;
        private readonly int _totalMessageCount;
        private readonly int _totalIndexedCount;

        public FakeAdministrationStore(
            bool enabled,
            int totalMessageCount = 0,
            int totalIndexedCount = 0)
        {
            _enabled = enabled;
            _totalMessageCount = totalMessageCount;
            _totalIndexedCount = totalIndexedCount;
        }

        public ValueTask<bool> IsEnabledAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_enabled);
        }

        public ValueTask<MessageIndexingAdministrationStatus> GetStatusAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(
                new MessageIndexingAdministrationStatus(
                    _totalMessageCount,
                    _totalIndexedCount,
                    _enabled,
                    IsFullTextReady: true,
                    QueuedMessageCount: 0,
                    LastError: string.Empty));
        }

        public ValueTask SetEnabledAsync(bool enabled, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask ClearAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask IndexAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask RebuildAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
