using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class PollingImapIdleNotifierTests
{
    [TestMethod]
    public async Task WatchAsync_YieldsExistsAndRecentWhenStatusChanges()
    {
        var store = new FakeDiscoveryStore(
            new ImapMailboxStatus(
                "INBOX",
                new Dictionary<ImapStatusItem, long>
                {
                    [ImapStatusItem.Messages] = 10,
                    [ImapStatusItem.Recent] = 2
                }));
        var notifier = new PollingImapIdleNotifier(
            store,
            new ImapIdlePollingOptions { PollInterval = TimeSpan.FromMilliseconds(1) });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using var enumerator = notifier
            .WatchAsync(
                new ImapIdleWatchRequest(77, 88, "INBOX", KnownExists: 9, KnownRecent: 1),
                cts.Token)
            .GetAsyncEnumerator(cts.Token);

        Assert.IsTrue(await enumerator.MoveNextAsync());
        Assert.AreEqual(ImapIdleEventKind.Exists, enumerator.Current.Kind);
        Assert.AreEqual(10, enumerator.Current.Number);

        Assert.IsTrue(await enumerator.MoveNextAsync());
        Assert.AreEqual(ImapIdleEventKind.Recent, enumerator.Current.Kind);
        Assert.AreEqual(2, enumerator.Current.Number);
    }

    private sealed class FakeDiscoveryStore : IImapMailboxDiscoveryStore
    {
        private readonly ImapMailboxStatus _status;

        public FakeDiscoveryStore(ImapMailboxStatus status)
        {
            _status = status;
        }

        public async IAsyncEnumerable<ImapMailboxListEntry> ListMailboxesAsync(
            int accountId,
            string referenceName,
            string mailboxPattern,
            bool subscribedOnly,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield break;
        }

        public ValueTask<ImapMailboxStatus?> GetStatusAsync(
            int accountId,
            string mailboxName,
            IReadOnlyList<ImapStatusItem> items,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<ImapMailboxStatus?>(_status);
    }
}
