using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapStoreAndExpungeCommandHandlerTests
{
    [TestMethod]
    public void StoreParser_ParsesUidSilentAddFlags()
    {
        var request = new ImapStoreCommandParser().Parse(
            accountId: 10,
            folderId: 20,
            arguments: "101:* +FLAGS.SILENT (\\Seen \\Deleted)",
            useUid: true);

        Assert.IsTrue(request.UseUid);
        Assert.AreEqual(ImapStoreMode.Add, request.Mode);
        Assert.IsTrue(request.Silent);
        Assert.AreEqual(ImapMessageFlags.Seen | ImapMessageFlags.Deleted, request.Flags);
        CollectionAssert.AreEqual(
            new[] { new ImapIdRange(101, null) },
            request.MessageSet.ToArray());
    }

    [TestMethod]
    public void StoreGetRequiredAclRightsMapsLegacyFlagGroups()
    {
        var handler = new ImapStoreCommandHandler(new ImapStoreCommandParser(), new CapturingMutationStore());

        var requiredRights = handler.GetRequiredAclRights(
            accountId: 10,
            folderId: 20,
            arguments: "101 +FLAGS (\\Seen \\Deleted \\Draft)",
            useUid: false);

        Assert.AreEqual(
            ImapAclRights.WriteSeen | ImapAclRights.WriteDeleted | ImapAclRights.WriteOthers,
            requiredRights);
    }

    [TestMethod]
    public void StoreParser_FlagsWithoutSeenUseSetModeForLegacyClearing()
    {
        var request = new ImapStoreCommandParser().Parse(
            accountId: 10,
            folderId: 20,
            arguments: "101 FLAGS (\\Flagged)",
            useUid: false);

        Assert.AreEqual(ImapStoreMode.Set, request.Mode);
        Assert.AreEqual(ImapMessageFlags.Flagged, request.Flags);
        Assert.AreEqual(0, request.Flags & ImapMessageFlags.Seen);
    }

    [TestMethod]
    public async Task StoreHandleAsync_ReturnsUpdatedFlagsWhenNotSilent()
    {
        var mutationStore = new CapturingMutationStore
        {
            StoredMessages =
            [
                new ImapStoredMessage(
                    new MessageIdentity(1, 10, 20, 101),
                    SequenceNumber: 4,
                    Flags: ImapMessageFlags.Seen | ImapMessageFlags.Deleted)
            ]
        };
        var handler = new ImapStoreCommandHandler(new ImapStoreCommandParser(), mutationStore);

        var response = await handler.HandleAsync(
            accountId: 10,
            folderId: 20,
            tag: "A001",
            arguments: "101 +FLAGS (\\Seen \\Deleted)",
            useUid: true,
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("* 4 FETCH (FLAGS (\\Deleted \\Seen) UID 101)\r\nA001 OK STORE completed\r\n", response);
        Assert.IsNotNull(mutationStore.LastStoreRequest);
        Assert.AreEqual(ImapStoreMode.Add, mutationStore.LastStoreRequest.Mode);
    }

    [TestMethod]
    public async Task StoreHandleAsync_SilentSuppressesFetchResponses()
    {
        var mutationStore = new CapturingMutationStore
        {
            StoredMessages =
            [
                new ImapStoredMessage(
                    new MessageIdentity(1, 10, 20, 101),
                    SequenceNumber: 4,
                    Flags: ImapMessageFlags.Seen)
            ]
        };
        var handler = new ImapStoreCommandHandler(new ImapStoreCommandParser(), mutationStore);

        var response = await handler.HandleAsync(
            accountId: 10,
            folderId: 20,
            tag: "A002",
            arguments: "101 +FLAGS.SILENT (\\Seen)",
            useUid: true,
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("A002 OK STORE completed\r\n", response);
    }

    [TestMethod]
    public async Task ExpungeHandleAsync_ReturnsExpungedSequenceNumbers()
    {
        var mutationStore = new CapturingMutationStore
        {
            ExpungedMessages =
            [
                new ImapExpungedMessage(new MessageIdentity(1, 10, 20, 101), SequenceNumber: 2),
                new ImapExpungedMessage(new MessageIdentity(2, 10, 20, 103), SequenceNumber: 3)
            ]
        };
        var handler = new ImapExpungeCommandHandler(mutationStore);

        var response = await handler.HandleAsync(
            accountId: 10,
            folderId: 20,
            tag: "A003",
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("* 2 EXPUNGE\r\n* 3 EXPUNGE\r\nA003 OK EXPUNGE completed\r\n", response);
        Assert.AreEqual(10, mutationStore.LastExpungeAccountId);
        Assert.AreEqual(20, mutationStore.LastExpungeFolderId);
    }

    private sealed class CapturingMutationStore : IImapMessageMutationStore
    {
        public IReadOnlyList<ImapStoredMessage> StoredMessages { get; init; } = Array.Empty<ImapStoredMessage>();

        public IReadOnlyList<ImapExpungedMessage> ExpungedMessages { get; init; } = Array.Empty<ImapExpungedMessage>();

        public ImapStoreRequest? LastStoreRequest { get; private set; }

        public int LastExpungeAccountId { get; private set; }

        public int LastExpungeFolderId { get; private set; }

        public async IAsyncEnumerable<ImapStoredMessage> StoreFlagsAsync(
            ImapStoreRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastStoreRequest = request;
            await Task.Yield();
            foreach (var message in StoredMessages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return message;
            }
        }

        public async IAsyncEnumerable<ImapExpungedMessage> ExpungeDeletedAsync(
            int accountId,
            int folderId,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastExpungeAccountId = accountId;
            LastExpungeFolderId = folderId;
            await Task.Yield();
            foreach (var message in ExpungedMessages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return message;
            }
        }
    }
}
