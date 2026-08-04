using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapQuotaCommandHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_ReturnsGetQuota()
    {
        var store = new FakeQuotaStore();
        var handler = new ImapQuotaCommandHandler(store);

        var response = await handler.HandleAsync(
            requesterAccountId: 77,
            tag: "A001",
            command: "GETQUOTA",
            arguments: "\"\"",
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("* QUOTA \"\" (STORAGE 2048 10240)\r\nA001 OK GETQUOTA completed\r\n", response);
        Assert.AreEqual(77, store.LastRequesterAccountId);
        Assert.AreEqual(string.Empty, store.LastQuotaRoot);
    }

    [TestMethod]
    public async Task HandleAsync_ReturnsGetQuotaRoot()
    {
        var handler = new ImapQuotaCommandHandler(new FakeQuotaStore());

        var response = await handler.HandleAsync(
            requesterAccountId: 77,
            tag: "A002",
            command: "GETQUOTAROOT",
            arguments: "\"INBOX\"",
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("* QUOTAROOT \"INBOX\" \"\"\r\n* QUOTA \"\" (STORAGE 2048 10240)\r\nA002 OK GETQUOTAROOT completed\r\n", response);
    }

    [TestMethod]
    public async Task HandleAsync_ReturnsLegacyEmptyResourceListWhenGetQuotaRootHasNoLimit()
    {
        var store = new FakeQuotaStore { ReturnNoLimit = true };
        var handler = new ImapQuotaCommandHandler(store);

        var response = await handler.HandleAsync(
            requesterAccountId: 77,
            tag: "A005",
            command: "GETQUOTAROOT",
            arguments: "\"INBOX\"",
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("* QUOTAROOT \"INBOX\" \"\"\r\n* QUOTA \"\" ()\r\nA005 OK GETQUOTAROOT completed\r\n", response);
    }

    [TestMethod]
    public async Task HandleAsync_PreservesLegacyAtomMailboxTokenForGetQuotaRoot()
    {
        var handler = new ImapQuotaCommandHandler(new FakeQuotaStore());

        var response = await handler.HandleAsync(
            requesterAccountId: 77,
            tag: "A006",
            command: "GETQUOTAROOT",
            arguments: "INBOX",
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("* QUOTAROOT INBOX \"\"\r\n* QUOTA \"\" (STORAGE 2048 10240)\r\nA006 OK GETQUOTAROOT completed\r\n", response);
    }

    [TestMethod]
    public async Task HandleAsync_ParsesSetQuotaStorageLimit()
    {
        var store = new FakeQuotaStore();
        var handler = new ImapQuotaCommandHandler(store);

        var response = await handler.HandleAsync(
            requesterAccountId: 77,
            tag: "A003",
            command: "SETQUOTA",
            arguments: "\"\" (STORAGE 5120)",
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("A003 OK SETQUOTA completed\r\n", response);
        Assert.AreEqual(5120, store.LastLimitKilobytes);
    }

    [TestMethod]
    public async Task HandleAsync_ReturnsBadForUnsupportedSetQuotaResource()
    {
        var handler = new ImapQuotaCommandHandler(new FakeQuotaStore());

        var response = await handler.HandleAsync(
            requesterAccountId: 77,
            tag: "A004",
            command: "SETQUOTA",
            arguments: "\"\" (MESSAGE 10)",
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("A004 BAD SETQUOTA supports only STORAGE with a non-negative limit.\r\n", response);
    }

    private sealed class FakeQuotaStore : IImapQuotaStore
    {
        public int LastRequesterAccountId { get; private set; }

        public string? LastQuotaRoot { get; private set; }

        public long LastLimitKilobytes { get; private set; }

        public bool ReturnNoLimit { get; init; }

        public ValueTask<ImapQuotaResult> GetQuotaAsync(
            int requesterAccountId,
            string quotaRoot,
            CancellationToken cancellationToken)
        {
            LastRequesterAccountId = requesterAccountId;
            LastQuotaRoot = quotaRoot;
            return ValueTask.FromResult(
                new ImapQuotaResult(
                    ImapQuotaCommandStatus.Success,
                    new ImapQuota(quotaRoot, UsedKilobytes: 2048, LimitKilobytes: ReturnNoLimit ? null : 10240)));
        }

        public ValueTask<ImapQuotaRootResult> GetQuotaRootAsync(
            int requesterAccountId,
            string mailboxName,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(
                new ImapQuotaRootResult(
                    ImapQuotaCommandStatus.Success,
                    mailboxName,
                    new ImapQuota(string.Empty, UsedKilobytes: 2048, LimitKilobytes: ReturnNoLimit ? null : 10240)));

        public ValueTask<ImapQuotaMutationResult> SetQuotaAsync(
            int requesterAccountId,
            string quotaRoot,
            long limitKilobytes,
            CancellationToken cancellationToken)
        {
            LastRequesterAccountId = requesterAccountId;
            LastQuotaRoot = quotaRoot;
            LastLimitKilobytes = limitKilobytes;
            return ValueTask.FromResult(new ImapQuotaMutationResult(ImapQuotaCommandStatus.Success));
        }
    }
}
