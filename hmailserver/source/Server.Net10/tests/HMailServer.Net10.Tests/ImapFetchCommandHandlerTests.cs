using System.Runtime.CompilerServices;
using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapFetchCommandHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_ReturnsMetadataAndBodyLiteral()
    {
        var store = new CapturingFetchStore(
        [
            new ImapFetchedMessage(
                new MessageIdentity(1, 10, 20, 101),
                SequenceNumber: 7,
                Flags: ImapMessageFlags.Seen,
                SizeBytes: 42,
                InternalDateUtc: new DateTimeOffset(2026, 6, 15, 12, 34, 56, TimeSpan.Zero),
                RawMessage: Encoding.ASCII.GetBytes("Hello"))
        ]);
        var handler = new ImapFetchCommandHandler(new ImapFetchCommandParser(), store);

        var response = await handler.HandleAsync(
            accountId: 10,
            folderId: 20,
            tag: "A001",
            arguments: "101 (FLAGS UID RFC822.SIZE INTERNALDATE BODY.PEEK[])",
            useUid: true,
            cancellationToken: CancellationToken.None);

        var text = Encoding.ASCII.GetString(response);
        Assert.AreEqual(
            "* 7 FETCH (FLAGS (\\Seen) UID 101 RFC822.SIZE 42 INTERNALDATE \"15-Jun-2026 12:34:56 +0000\" BODY[] {5}\r\nHello)\r\nA001 OK FETCH completed\r\n",
            text);
        Assert.IsNotNull(store.LastRequest);
        Assert.IsTrue(store.LastRequest.UseUid);
        Assert.IsTrue(store.LastRequest.RequiresRawMessage);
    }

    [TestMethod]
    public async Task HandleAsync_ReturnsTaggedBadForUnsupportedItem()
    {
        var handler = new ImapFetchCommandHandler(
            new ImapFetchCommandParser(),
            new CapturingFetchStore(Array.Empty<ImapFetchedMessage>()));

        var response = await handler.HandleAsync(
            accountId: 10,
            folderId: 20,
            tag: "A002",
            arguments: "1 (BODY[]<0.1024>)",
            useUid: false,
            cancellationToken: CancellationToken.None);

        var text = Encoding.ASCII.GetString(response);
        Assert.IsTrue(text.StartsWith("A002 BAD Unsupported FETCH data item", StringComparison.Ordinal), text);
    }

    [TestMethod]
    public async Task HandleAsync_ReturnsEnvelopeAndBodyStructure()
    {
        var rawMessage = Encoding.UTF8.GetBytes(
            "Date: Mon, 15 Jun 2026 12:34:56 +0000\r\n" +
            "Subject: Quarterly Report\r\n" +
            "From: Ada Lovelace <ada@example.test>\r\n" +
            "To: Bob Example <bob@example.test>\r\n" +
            "Message-Id: <m1@example.test>\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            "\r\n" +
            "Hello\r\n");
        var store = new CapturingFetchStore(
        [
            new ImapFetchedMessage(
                new MessageIdentity(1, 10, 20, 101),
                SequenceNumber: 1,
                Flags: 0,
                SizeBytes: rawMessage.Length,
                InternalDateUtc: new DateTimeOffset(2026, 6, 15, 12, 34, 56, TimeSpan.Zero),
                RawMessage: rawMessage)
        ]);
        var handler = new ImapFetchCommandHandler(new ImapFetchCommandParser(), store);

        var response = await handler.HandleAsync(
            accountId: 10,
            folderId: 20,
            tag: "A003",
            arguments: "101 (UID ENVELOPE BODYSTRUCTURE)",
            useUid: true,
            cancellationToken: CancellationToken.None);

        var text = Encoding.ASCII.GetString(response);
        StringAssert.Contains(text, "UID 101 ENVELOPE");
        StringAssert.Contains(text, "\"Quarterly Report\"");
        StringAssert.Contains(text, "\"ada\" \"example.test\"");
        StringAssert.Contains(text, "\"bob\" \"example.test\"");
        StringAssert.Contains(text, "BODYSTRUCTURE (\"TEXT\" \"PLAIN\"");
        StringAssert.Contains(text, "(\"CHARSET\" \"utf-8\")");
        StringAssert.Contains(text, "A003 OK FETCH completed\r\n");
        Assert.IsNotNull(store.LastRequest);
        Assert.IsTrue(store.LastRequest.RequiresRawMessage);
    }

    [TestMethod]
    public void FetchParser_FullIncludesBodyAndMarksSeen()
    {
        var request = new ImapFetchCommandParser().Parse(10, 20, "1 FULL", useUid: false);

        CollectionAssert.Contains(request.Items.ToArray(), ImapFetchDataItem.Body);
        Assert.IsTrue(request.MarksSeen);
    }

    [TestMethod]
    public async Task HandleAsync_BodyMarksOnlyUnseenMessagesWhenMailboxAllowsWriteSeen()
    {
        var fetchStore = new CapturingFetchStore(
        [
            new ImapFetchedMessage(
                new MessageIdentity(1, 10, 20, 101),
                SequenceNumber: 1,
                Flags: 0,
                SizeBytes: 5,
                InternalDateUtc: DateTimeOffset.UtcNow,
                RawMessage: Encoding.ASCII.GetBytes("Hello")),
            new ImapFetchedMessage(
                new MessageIdentity(2, 10, 20, 102),
                SequenceNumber: 2,
                Flags: ImapMessageFlags.Seen,
                SizeBytes: 5,
                InternalDateUtc: DateTimeOffset.UtcNow,
                RawMessage: Encoding.ASCII.GetBytes("World"))
        ]);
        var mutationStore = new CapturingMutationStore();
        var handler = new ImapFetchCommandHandler(new ImapFetchCommandParser(), fetchStore, mutationStore);

        var response = await handler.HandleAsync(
            accountId: 10,
            folderId: 20,
            tag: "A004",
            arguments: "1:2 BODY[]",
            useUid: false,
            cancellationToken: CancellationToken.None,
            isReadOnly: false,
            aclRights: ImapAclRights.All);

        StringAssert.Contains(Encoding.ASCII.GetString(response), "A004 OK FETCH completed\r\n");
        Assert.IsNotNull(mutationStore.LastStoreRequest);
        Assert.IsTrue(mutationStore.LastStoreRequest.UseUid);
        Assert.AreEqual(ImapStoreMode.Add, mutationStore.LastStoreRequest.Mode);
        Assert.AreEqual(ImapMessageFlags.Seen, mutationStore.LastStoreRequest.Flags);
        Assert.IsTrue(mutationStore.LastStoreRequest.Silent);
        CollectionAssert.AreEqual(
            new[] { new ImapIdRange(101, 101) },
            mutationStore.LastStoreRequest.MessageSet.ToArray());
    }

    [TestMethod]
    public async Task HandleAsync_BodyPeekDoesNotMarkSeen()
    {
        var mutationStore = new CapturingMutationStore();
        var handler = new ImapFetchCommandHandler(
            new ImapFetchCommandParser(),
            new CapturingFetchStore(
            [
                new ImapFetchedMessage(
                    new MessageIdentity(1, 10, 20, 101),
                    SequenceNumber: 1,
                    Flags: 0,
                    SizeBytes: 5,
                    InternalDateUtc: DateTimeOffset.UtcNow,
                    RawMessage: Encoding.ASCII.GetBytes("Hello"))
            ]),
            mutationStore);

        await handler.HandleAsync(
            accountId: 10,
            folderId: 20,
            tag: "A005",
            arguments: "1 BODY.PEEK[]",
            useUid: false,
            cancellationToken: CancellationToken.None,
            isReadOnly: false,
            aclRights: ImapAclRights.All);

        Assert.IsNull(mutationStore.LastStoreRequest);
    }

    [TestMethod]
    public async Task HandleAsync_BodyWithoutWriteSeenDoesNotMarkSeen()
    {
        var mutationStore = new CapturingMutationStore();
        var handler = new ImapFetchCommandHandler(
            new ImapFetchCommandParser(),
            new CapturingFetchStore(
            [
                new ImapFetchedMessage(
                    new MessageIdentity(1, 10, 20, 101),
                    SequenceNumber: 1,
                    Flags: 0,
                    SizeBytes: 5,
                    InternalDateUtc: DateTimeOffset.UtcNow,
                    RawMessage: Encoding.ASCII.GetBytes("Hello"))
            ]),
            mutationStore);

        await handler.HandleAsync(
            accountId: 10,
            folderId: 20,
            tag: "A006",
            arguments: "1 BODY[]",
            useUid: false,
            cancellationToken: CancellationToken.None,
            isReadOnly: false,
            aclRights: ImapAclRights.All & ~ImapAclRights.WriteSeen);

        Assert.IsNull(mutationStore.LastStoreRequest);
    }

    private sealed class CapturingFetchStore : IImapMessageFetchStore
    {
        private readonly IReadOnlyList<ImapFetchedMessage> _messages;

        public CapturingFetchStore(IReadOnlyList<ImapFetchedMessage> messages)
        {
            _messages = messages;
        }

        public ImapFetchRequest? LastRequest { get; private set; }

        public async IAsyncEnumerable<ImapFetchedMessage> FetchAsync(
            ImapFetchRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastRequest = request;
            await Task.Yield();
            foreach (var message in _messages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return message;
            }
        }
    }

    private sealed class CapturingMutationStore : IImapMessageMutationStore
    {
        public ImapStoreRequest? LastStoreRequest { get; private set; }

        public async IAsyncEnumerable<ImapStoredMessage> StoreFlagsAsync(
            ImapStoreRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastStoreRequest = request;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield break;
        }

        public async IAsyncEnumerable<ImapExpungedMessage> ExpungeDeletedAsync(
            int accountId,
            int folderId,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield break;
        }
    }
}
