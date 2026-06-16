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

    private sealed class FakeMessageSearchIndex : IMessageSearchIndex
    {
        private readonly IReadOnlyList<MessageIdentity> _identities;

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
}
