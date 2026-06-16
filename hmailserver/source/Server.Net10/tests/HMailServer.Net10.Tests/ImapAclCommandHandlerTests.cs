using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapAclCommandHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_ReturnsGetAclEntries()
    {
        var store = new FakeAclStore();
        var handler = new ImapAclCommandHandler(store);

        var response = await handler.HandleAsync(
            requesterAccountId: 77,
            tag: "A001",
            command: "GETACL",
            arguments: "\"#Public.Shared\"",
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("* ACL \"#Public.Shared\" user@example.test lrw Anyone l\r\nA001 OK GETACL completed\r\n", response);
        Assert.AreEqual(77, store.LastRequesterAccountId);
        Assert.AreEqual("#Public.Shared", store.LastMailboxName);
    }

    [TestMethod]
    public async Task HandleAsync_ReturnsMyRights()
    {
        var handler = new ImapAclCommandHandler(new FakeAclStore());

        var response = await handler.HandleAsync(
            requesterAccountId: 77,
            tag: "A002",
            command: "MYRIGHTS",
            arguments: "\"#Public.Shared\"",
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("* MYRIGHTS \"#Public.Shared\" lra\r\nA002 OK MYRIGHTS completed\r\n", response);
    }

    [TestMethod]
    public async Task HandleAsync_ReturnsListRights()
    {
        var handler = new ImapAclCommandHandler(new FakeAclStore());

        var response = await handler.HandleAsync(
            requesterAccountId: 77,
            tag: "A003",
            command: "LISTRIGHTS",
            arguments: "\"#Public.Shared\" user@example.test",
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("* LISTRIGHTS \"#Public.Shared\" user@example.test l r s w i k x t e a\r\nA003 OK LISTRIGHTS completed\r\n", response);
    }

    [TestMethod]
    public async Task HandleAsync_ParsesSetAclRightsChange()
    {
        var store = new FakeAclStore();
        var handler = new ImapAclCommandHandler(store);

        var response = await handler.HandleAsync(
            requesterAccountId: 77,
            tag: "A004",
            command: "SETACL",
            arguments: "\"#Public.Shared\" user@example.test +st",
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("A004 OK SETACL completed\r\n", response);
        Assert.IsNotNull(store.LastRightsChange);
        Assert.AreEqual(ImapAclRightsChangeMode.Add, store.LastRightsChange.Mode);
        Assert.AreEqual(ImapAclRights.WriteSeen | ImapAclRights.WriteDeleted, store.LastRightsChange.Rights);
    }

    [TestMethod]
    public async Task HandleAsync_ReturnsBadForInvalidRights()
    {
        var store = new FakeAclStore();
        var handler = new ImapAclCommandHandler(store);

        var response = await handler.HandleAsync(
            requesterAccountId: 77,
            tag: "A005",
            command: "SETACL",
            arguments: "\"#Public.Shared\" user@example.test z",
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("A005 BAD SETACL contains an invalid access right\r\n", response);
        Assert.IsNull(store.LastRightsChange);
    }

    private sealed class FakeAclStore : IImapAclStore
    {
        public int LastRequesterAccountId { get; private set; }

        public string? LastMailboxName { get; private set; }

        public ImapAclRightsChange? LastRightsChange { get; private set; }

        public ValueTask<ImapAclListResult> GetAclAsync(
            int requesterAccountId,
            string mailboxName,
            CancellationToken cancellationToken)
        {
            LastRequesterAccountId = requesterAccountId;
            LastMailboxName = mailboxName;
            return ValueTask.FromResult(
                new ImapAclListResult(
                    ImapAclCommandStatus.Success,
                    mailboxName,
                    [
                        new ImapAclEntry("user@example.test", "lrw"),
                        new ImapAclEntry("Anyone", "l")
                    ]));
        }

        public ValueTask<ImapAclRightsResult> GetMyRightsAsync(
            int requesterAccountId,
            string mailboxName,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(
                new ImapAclRightsResult(
                    ImapAclCommandStatus.Success,
                    mailboxName,
                    "lra"));

        public ValueTask<ImapAclMutationResult> SetAclAsync(
            int requesterAccountId,
            string mailboxName,
            string identifier,
            ImapAclRightsChange rightsChange,
            CancellationToken cancellationToken)
        {
            LastRequesterAccountId = requesterAccountId;
            LastMailboxName = mailboxName;
            LastRightsChange = rightsChange;
            return ValueTask.FromResult(new ImapAclMutationResult(ImapAclCommandStatus.Success));
        }

        public ValueTask<ImapAclMutationResult> DeleteAclAsync(
            int requesterAccountId,
            string mailboxName,
            string identifier,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ImapAclMutationResult(ImapAclCommandStatus.Success));
    }
}
