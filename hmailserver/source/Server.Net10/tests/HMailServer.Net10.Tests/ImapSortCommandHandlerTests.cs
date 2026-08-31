using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapSortCommandHandlerTests
{
    [TestMethod]
    public void Parse_SupportsReverseCriteriaCharsetAndSearchCriteria()
    {
        var parser = new ImapSortCommandParser(new ImapSearchCommandParser());

        var request = parser.Parse(
            accountId: 10,
            folderId: 20,
            arguments: "(REVERSE DATE SUBJECT) UTF-8 UNSEEN TEXT \"invoice\"",
            returnUid: true);

        Assert.IsTrue(request.ReturnUid);
        Assert.AreEqual(ImapSortKey.Date, request.Criteria[0].Key);
        Assert.IsTrue(request.Criteria[0].Descending);
        Assert.AreEqual(ImapSortKey.Subject, request.Criteria[1].Key);
        Assert.IsFalse(request.Criteria[1].Descending);
        Assert.AreEqual(ImapMessageFlags.Seen, request.SearchRequest.ForbiddenFlags);
        CollectionAssert.AreEqual(new[] { "invoice" }, request.SearchRequest.GetAnyTerms().ToArray());
    }

    [TestMethod]
    public async Task HandleAsync_ReturnsUidSortResponseAndTaggedOk()
    {
        var index = new CapturingSortIndex(
        [
            new MessageIdentity(2, 10, 20, 105),
            new MessageIdentity(1, 10, 20, 101)
        ]);
        var handler = CreateHandler(index);

        var response = await handler.HandleAsync(
            accountId: 10,
            folderId: 20,
            tag: "A001",
            arguments: "(REVERSE DATE SUBJECT) UTF-8 UNSEEN TEXT \"invoice\"",
            returnUid: true,
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("* SORT 105 101\r\nA001 OK Search completed\r\n", response);
        Assert.IsNotNull(index.LastRequest);
        Assert.IsTrue(index.LastRequest.ReturnUid);
        Assert.AreEqual(ImapSortKey.Date, index.LastRequest.Criteria[0].Key);
        Assert.IsTrue(index.LastRequest.Criteria[0].Descending);
        CollectionAssert.AreEqual(new[] { "invoice" }, index.LastRequest.SearchRequest.GetAnyTerms().ToArray());
    }

    [TestMethod]
    public async Task HandleAsync_ResolvesNonUidSortToSequenceNumbers()
    {
        var index = new CapturingSortIndex(
        [
            new MessageIdentity(2, 10, 20, 105),
            new MessageIdentity(1, 10, 20, 101)
        ]);
        var handler = CreateHandler(index);

        var response = await handler.HandleAsync(
            accountId: 10,
            folderId: 20,
            tag: "A002",
            arguments: "(SUBJECT) US-ASCII ALL",
            returnUid: false,
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("* SORT 4 9\r\nA002 OK Search completed\r\n", response);
    }

    [TestMethod]
    public async Task HandleAsync_ReturnsTaggedBadForUnsupportedCharset()
    {
        var handler = CreateHandler(new CapturingSortIndex(Array.Empty<MessageIdentity>()));

        var response = await handler.HandleAsync(
            accountId: 10,
            folderId: 20,
            tag: "A003",
            arguments: "(DATE) KOI8-R ALL",
            returnUid: true,
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("A003 BAD Unsupported SORT CHARSET 'KOI8-R'.\r\n", response);
    }

    private static ImapSortCommandHandler CreateHandler(CapturingSortIndex index) =>
        new(
            new ImapSortCommandParser(new ImapSearchCommandParser()),
            new ImapSortExecutor(index, new SnapshotSequenceNumberResolver()));

    private sealed class CapturingSortIndex : IMessageSortIndex
    {
        private readonly IReadOnlyList<MessageIdentity> _identities;

        public CapturingSortIndex(IReadOnlyList<MessageIdentity> identities)
        {
            _identities = identities;
        }

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

    private sealed class SnapshotSequenceNumberResolver : IImapSequenceNumberResolver
    {
        public ValueTask<IReadOnlyDictionary<long, long>> ResolveMailboxSequenceNumbersAsync(
            int accountId,
            int folderId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyDictionary<long, long>>(
                new Dictionary<long, long>
                {
                    [1] = 9,
                    [2] = 4
                });
    }
}
