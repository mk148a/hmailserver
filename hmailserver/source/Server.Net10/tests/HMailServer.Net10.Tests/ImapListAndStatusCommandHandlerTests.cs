using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapListAndStatusCommandHandlerTests
{
    [TestMethod]
    public async Task ListHandleAsync_ReturnsMailboxEntriesAndTaggedOk()
    {
        var store = new CapturingDiscoveryStore(
        [
            new ImapMailboxListEntry("INBOX", HasChildren: false, IsSelectable: true, IsSubscribed: true),
            new ImapMailboxListEntry("Projects", HasChildren: true, IsSelectable: true, IsSubscribed: false)
        ]);
        var handler = new ImapListCommandHandler(store, ".");

        var response = await handler.HandleAsync(
            accountId: 77,
            tag: "A001",
            arguments: "\"\" \"*\"",
            subscribedOnly: false,
            cancellationToken: CancellationToken.None);

        Assert.AreEqual(
            "* LIST (\\HasNoChildren) \".\" \"INBOX\"\r\n* LIST (\\HasChildren) \".\" \"Projects\"\r\nA001 OK LIST completed\r\n",
            response);
        Assert.AreEqual(77, store.LastListAccountId);
        Assert.AreEqual(string.Empty, store.LastReferenceName);
        Assert.AreEqual("*", store.LastMailboxPattern);
        Assert.IsFalse(store.LastSubscribedOnly);
    }

    [TestMethod]
    public async Task ListHandleAsync_ReturnsRootForEmptyListPattern()
    {
        var handler = new ImapListCommandHandler(
            new CapturingDiscoveryStore(Array.Empty<ImapMailboxListEntry>()),
            ".");

        var response = await handler.HandleAsync(
            accountId: 77,
            tag: "A002",
            arguments: "\"\" \"\"",
            subscribedOnly: false,
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("* LIST (\\Noselect) \".\" \"\"\r\nA002 OK LIST completed\r\n", response);
    }

    [TestMethod]
    public void StatusParser_ParsesParenthesizedItems()
    {
        var command = new ImapStatusCommandParser().Parse(
            "\"INBOX\" (MESSAGES UNSEEN UIDNEXT)");

        Assert.AreEqual("INBOX", command.MailboxName);
        CollectionAssert.AreEqual(
            new[]
            {
                ImapStatusItem.Messages,
                ImapStatusItem.Unseen,
                ImapStatusItem.UidNext
            },
            command.Items.ToArray());
    }

    [TestMethod]
    public async Task StatusHandleAsync_ReturnsRequestedCounters()
    {
        var store = new CapturingDiscoveryStore(Array.Empty<ImapMailboxListEntry>())
        {
            Status = new ImapMailboxStatus(
                "INBOX",
                new Dictionary<ImapStatusItem, long>
                {
                    [ImapStatusItem.Messages] = 9,
                    [ImapStatusItem.Unseen] = 3,
                    [ImapStatusItem.UidNext] = 500
                })
        };
        var handler = new ImapStatusCommandHandler(new ImapStatusCommandParser(), store);

        var response = await handler.HandleAsync(
            accountId: 77,
            tag: "A003",
            arguments: "\"INBOX\" (MESSAGES UNSEEN UIDNEXT)",
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("* STATUS \"INBOX\" (MESSAGES 9 UNSEEN 3 UIDNEXT 500)\r\nA003 OK STATUS completed\r\n", response);
        Assert.AreEqual("INBOX", store.LastStatusMailboxName);
    }

    private sealed class CapturingDiscoveryStore : IImapMailboxDiscoveryStore
    {
        private readonly IReadOnlyList<ImapMailboxListEntry> _entries;

        public CapturingDiscoveryStore(IReadOnlyList<ImapMailboxListEntry> entries)
        {
            _entries = entries;
        }

        public int LastListAccountId { get; private set; }

        public string? LastReferenceName { get; private set; }

        public string? LastMailboxPattern { get; private set; }

        public bool LastSubscribedOnly { get; private set; }

        public string? LastStatusMailboxName { get; private set; }

        public ImapMailboxStatus? Status { get; init; }

        public async IAsyncEnumerable<ImapMailboxListEntry> ListMailboxesAsync(
            int accountId,
            string referenceName,
            string mailboxPattern,
            bool subscribedOnly,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastListAccountId = accountId;
            LastReferenceName = referenceName;
            LastMailboxPattern = mailboxPattern;
            LastSubscribedOnly = subscribedOnly;
            await Task.Yield();
            foreach (var entry in _entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return entry;
            }
        }

        public ValueTask<ImapMailboxStatus?> GetStatusAsync(
            int accountId,
            string mailboxName,
            IReadOnlyList<ImapStatusItem> items,
            CancellationToken cancellationToken)
        {
            LastStatusMailboxName = mailboxName;
            return ValueTask.FromResult(Status);
        }
    }
}
