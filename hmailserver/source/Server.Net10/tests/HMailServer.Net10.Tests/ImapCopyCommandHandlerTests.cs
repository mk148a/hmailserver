using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapCopyCommandHandlerTests
{
    [TestMethod]
    public void CopyParser_ParsesMessageSetAndDestination()
    {
        var command = new ImapCopyCommandParser().Parse("101:* \"Archive.2026\"");

        Assert.AreEqual("Archive.2026", command.DestinationMailbox);
        CollectionAssert.AreEqual(
            new[] { new ImapIdRange(101, null) },
            command.MessageSet.ToArray());
    }

    [TestMethod]
    public async Task HandleAsync_CopiesToResolvedDestinationMailbox()
    {
        var mailboxStore = new FakeMailboxStore();
        var copyStore = new CapturingCopyStore();
        var handler = new ImapCopyCommandHandler(new ImapCopyCommandParser(), mailboxStore, copyStore);

        var response = await handler.HandleAsync(
            requesterAccountId: 77,
            sourceAccountId: 77,
            sourceFolderId: 88,
            tag: "A001",
            arguments: "101 \"Archive\"",
            useUid: true,
            deleteSource: false,
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("A001 OK COPY completed\r\n", response);
        Assert.IsNotNull(copyStore.LastRequest);
        Assert.AreEqual(77, copyStore.LastRequest.SourceAccountId);
        Assert.AreEqual(88, copyStore.LastRequest.SourceFolderId);
        Assert.AreEqual(77, copyStore.LastRequest.DestinationAccountId);
        Assert.AreEqual(99, copyStore.LastRequest.DestinationFolderId);
        Assert.IsTrue(copyStore.LastRequest.UseUid);
        Assert.IsFalse(copyStore.LastRequest.DeleteSource);
    }

    [TestMethod]
    public async Task HandleAsync_MoveReturnsExpungeResponses()
    {
        var copyStore = new CapturingCopyStore
        {
            CopiedMessages =
            [
                new ImapCopiedMessage(
                    new MessageIdentity(1, 77, 88, 101),
                    4,
                    new MessageIdentity(2, 77, 99, 201),
                    4)
            ]
        };
        var handler = new ImapCopyCommandHandler(new ImapCopyCommandParser(), new FakeMailboxStore(), copyStore);

        var response = await handler.HandleAsync(
            requesterAccountId: 77,
            sourceAccountId: 77,
            sourceFolderId: 88,
            tag: "A002",
            arguments: "4 \"Archive\"",
            useUid: false,
            deleteSource: true,
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("* 4 EXPUNGE\r\nA002 OK MOVE completed\r\n", response);
        Assert.IsNotNull(copyStore.LastRequest);
        Assert.IsTrue(copyStore.LastRequest.DeleteSource);
    }

    private sealed class FakeMailboxStore : IImapMailboxStore
    {
        public ValueTask<ImapMailboxSelection?> SelectMailboxAsync(
            int accountId,
            string mailboxName,
            bool readOnly,
            CancellationToken cancellationToken)
        {
            if (accountId == 77 && mailboxName.Equals("Archive", StringComparison.OrdinalIgnoreCase))
            {
                return ValueTask.FromResult<ImapMailboxSelection?>(
                    new ImapMailboxSelection(
                        AccountId: accountId,
                        FolderId: 99,
                        Name: mailboxName,
                        Exists: 0,
                        Recent: 0,
                        UidValidity: 123,
                        UidNext: 201,
                        FirstUnseenUid: null,
                        IsReadOnly: readOnly));
            }

            return ValueTask.FromResult<ImapMailboxSelection?>(null);
        }
    }

    private sealed class CapturingCopyStore : IImapMessageCopyStore
    {
        public IReadOnlyList<ImapCopiedMessage> CopiedMessages { get; init; } = Array.Empty<ImapCopiedMessage>();

        public ImapCopyRequest? LastRequest { get; private set; }

        public async IAsyncEnumerable<ImapCopiedMessage> CopyAsync(
            ImapCopyRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastRequest = request;
            await Task.Yield();
            foreach (var message in CopiedMessages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return message;
            }
        }
    }
}
