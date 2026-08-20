using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapAppendCommandHandlerTests
{
    [TestMethod]
    public void AppendParser_ParsesMailboxFlagsAndLiteralCount()
    {
        var command = new ImapAppendCommandParser().Parse("\"INBOX\" (\\Seen \\Flagged) {12}");

        Assert.AreEqual("INBOX", command.MailboxName);
        Assert.AreEqual(ImapMessageFlags.Seen | ImapMessageFlags.Flagged, command.Flags);
        Assert.AreEqual(12, command.LiteralByteCount);
    }

    [TestMethod]
    public async Task HandleAsync_AppendsToResolvedMailbox()
    {
        var appendStore = new FakeAppendStore();
        var handler = new ImapAppendCommandHandler(
            new ImapAppendCommandParser(),
            new FakeMailboxStore(),
            appendStore);
        var command = new ImapAppendCommand("INBOX", ImapMessageFlags.Seen, null, 5);

        var response = await handler.HandleAsync(
            requesterAccountId: 77,
            tag: "A001",
            command,
            "Hello"u8.ToArray(),
            CancellationToken.None);

        Assert.AreEqual("A001 OK [APPENDUID 123 501] APPEND completed\r\n", response);
        Assert.IsNotNull(appendStore.LastRequest);
        Assert.AreEqual(77, appendStore.LastRequest.DestinationAccountId);
        Assert.AreEqual(88, appendStore.LastRequest.DestinationFolderId);
        Assert.AreEqual(ImapMessageFlags.Seen, appendStore.LastRequest.Flags);
        CollectionAssert.AreEqual("Hello"u8.ToArray(), appendStore.LastRequest.RawMessage);
    }

    [TestMethod]
    public async Task HandleAsync_RemovesSeenWhenDestinationLacksWriteSeenAcl()
    {
        var appendStore = new FakeAppendStore();
        var handler = new ImapAppendCommandHandler(
            new ImapAppendCommandParser(),
            new FakeMailboxStore(ImapAclRights.All & ~ImapAclRights.WriteSeen),
            appendStore);
        var command = new ImapAppendCommand(
            "INBOX",
            ImapMessageFlags.Seen | ImapMessageFlags.Flagged,
            null,
            5);

        var response = await handler.HandleAsync(
            requesterAccountId: 77,
            tag: "A003",
            command,
            "Hello"u8.ToArray(),
            CancellationToken.None);

        Assert.AreEqual("A003 OK [APPENDUID 123 501] APPEND completed\r\n", response);
        Assert.IsNotNull(appendStore.LastRequest);
        Assert.AreEqual(ImapMessageFlags.Flagged, appendStore.LastRequest.Flags);
    }

    [TestMethod]
    public async Task HandleAsync_DoesNotAppendWhenDestinationLacksInsertAcl()
    {
        var appendStore = new FakeAppendStore();
        var handler = new ImapAppendCommandHandler(
            new ImapAppendCommandParser(),
            new FakeMailboxStore(ImapAclRights.All & ~ImapAclRights.Insert),
            appendStore);
        var command = new ImapAppendCommand("INBOX", ImapMessageFlags.Seen, null, 5);

        var response = await handler.HandleAsync(
            requesterAccountId: 77,
            tag: "A002",
            command,
            "Hello"u8.ToArray(),
            CancellationToken.None);

        Assert.AreEqual("A002 NO ACL: Insert permission denied (Required for APPEND command).\r\n", response);
        Assert.IsNull(appendStore.LastRequest);
    }

    private sealed class FakeMailboxStore(long aclRights = ImapAclRights.All) : IImapMailboxStore
    {
        public ValueTask<ImapMailboxSelection?> SelectMailboxAsync(
            int accountId,
            string mailboxName,
            bool readOnly,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<ImapMailboxSelection?>(
                new ImapMailboxSelection(
                    AccountId: accountId,
                    FolderId: 88,
                    Name: mailboxName,
                    Exists: 0,
                    Recent: 0,
                    UidValidity: 123,
                    UidNext: 501,
                    FirstUnseenUid: null,
                    IsReadOnly: false,
                    AclRights: aclRights));
    }

    private sealed class FakeAppendStore : IImapMessageAppendStore
    {
        public ImapAppendRequest? LastRequest { get; private set; }

        public ValueTask<ImapAppendResult> AppendAsync(
            ImapAppendRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return ValueTask.FromResult(
                new ImapAppendResult(
                    new MessageIdentity(10, request.DestinationAccountId, request.DestinationFolderId, 501),
                    UidValidity: 123));
        }
    }
}
