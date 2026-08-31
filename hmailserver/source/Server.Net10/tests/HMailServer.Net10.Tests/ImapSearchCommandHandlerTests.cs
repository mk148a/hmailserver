using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapSearchCommandHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_ReturnsSearchResponseAndTaggedOk()
    {
        var index = new CapturingSearchIndex(
        [
            new MessageIdentity(1, 10, 20, 101),
            new MessageIdentity(2, 10, 20, 105)
        ]);
        var handler = CreateHandler(index);

        var response = await handler.HandleAsync(
            accountId: 10,
            folderId: 20,
            tag: "A001",
            commandText: "UID SEARCH TEXT \"invoice\" UNSEEN",
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("* SEARCH 101 105\r\nA001 OK Search completed\r\n", response);
        Assert.IsNotNull(index.LastRequest);
        Assert.IsTrue(index.LastRequest.ReturnUid);
        CollectionAssert.AreEqual(new[] { "invoice" }, index.LastRequest.GetAnyTerms().ToArray());
        Assert.AreEqual(ImapMessageFlags.Seen, index.LastRequest.ForbiddenFlags);
    }

    [TestMethod]
    public async Task HandleAsync_ReturnsTaggedBadForUnsupportedSearchKey()
    {
        var handler = CreateHandler(new CapturingSearchIndex(Array.Empty<MessageIdentity>()));

        var response = await handler.HandleAsync(
            accountId: 10,
            folderId: 20,
            tag: "A002",
            commandText: "SEARCH OR FROM a@example.test TO b@example.test",
            cancellationToken: CancellationToken.None);

        Assert.IsTrue(
            response.StartsWith("A002 BAD Unsupported SEARCH key", StringComparison.Ordinal),
            response);
    }

    private static ImapSearchCommandHandler CreateHandler(CapturingSearchIndex index) =>
        new(
            new ImapSearchCommandParser(),
            new ImapSearchExecutor(index, new SnapshotSequenceNumberResolver()));

    private sealed class CapturingSearchIndex : IMessageSearchIndex
    {
        private readonly IReadOnlyList<MessageIdentity> _identities;

        public CapturingSearchIndex(IReadOnlyList<MessageIdentity> identities)
        {
            _identities = identities;
        }

        public ImapSearchRequest? LastRequest { get; private set; }

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

    private sealed class SnapshotSequenceNumberResolver : IImapSequenceNumberResolver
    {
        public ValueTask<IReadOnlyDictionary<long, long>> ResolveMailboxSequenceNumbersAsync(
            int accountId,
            int folderId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyDictionary<long, long>>(
                new Dictionary<long, long>
                {
                    [1] = 1,
                    [2] = 2
                });
    }
}
