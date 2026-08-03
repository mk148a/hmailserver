using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapSubscriptionCommandHandlerTests
{
    [TestMethod]
    public async Task Subscribe_UpdatesTheRequestedPrivateMailbox()
    {
        var store = new FakeStore(ImapMailboxSubscriptionStatus.Success);
        var handler = new ImapSubscriptionCommandHandler(store, "#Public");

        var response = await handler.HandleAsync(10, "A001", "SUBSCRIBE", "Projects extra", CancellationToken.None);

        Assert.AreEqual("A001 OK Subscribe completed\r\n", response);
        Assert.AreEqual(10, store.LastAccountId);
        Assert.AreEqual("Projects", store.LastMailboxName);
        Assert.IsTrue(store.LastSubscribed);
    }

    [TestMethod]
    public async Task Subscribe_PublicRootIsAcceptedWithoutPersistence()
    {
        var store = new FakeStore(ImapMailboxSubscriptionStatus.Failed);
        var handler = new ImapSubscriptionCommandHandler(store, "#Public");

        var response = await handler.HandleAsync(10, "A001", "SUBSCRIBE", "#Public", CancellationToken.None);

        Assert.AreEqual("A001 OK Subscribe completed\r\n", response);
        Assert.IsNull(store.LastMailboxName);
    }

    [TestMethod]
    public async Task Subscribe_MapsMissingAndLookupDeniedFolders()
    {
        var missingStore = new FakeStore(ImapMailboxSubscriptionStatus.MailboxNotFound);
        var missing = await new ImapSubscriptionCommandHandler(missingStore, "#Public")
            .HandleAsync(10, "A001", "SUBSCRIBE", "Missing", CancellationToken.None);
        Assert.AreEqual("A001 NO Folder could not be found.\r\n", missing);

        var deniedStore = new FakeStore(ImapMailboxSubscriptionStatus.PermissionDenied);
        var denied = await new ImapSubscriptionCommandHandler(deniedStore, "#Public")
            .HandleAsync(10, "A002", "SUBSCRIBE", "Hidden", CancellationToken.None);
        Assert.AreEqual("A002 NO ACL: Lookup permission denied (required for SUBSCRIBE).\r\n", denied);
    }

    [TestMethod]
    public async Task Unsubscribe_UpdatesPrivateMailboxAndRejectsPublicFolder()
    {
        var store = new FakeStore(ImapMailboxSubscriptionStatus.Success);
        var handler = new ImapSubscriptionCommandHandler(store, "#Public");

        var response = await handler.HandleAsync(10, "A001", "UNSUBSCRIBE", "Projects", CancellationToken.None);

        Assert.AreEqual("A001 OK Unsubscribe completed\r\n", response);
        Assert.IsFalse(store.LastSubscribed);

        store.Status = ImapMailboxSubscriptionStatus.PublicFolderNotSupported;
        var publicResponse = await handler.HandleAsync(10, "A002", "UNSUBSCRIBE", "#Public.Child", CancellationToken.None);
        Assert.AreEqual("A002 NO It is not possible to unsubscribe from public folders.\r\n", publicResponse);
    }

    [TestMethod]
    public async Task Unsubscribe_RequiresExactlyOneArgumentAndMapsMissingFolder()
    {
        var store = new FakeStore(ImapMailboxSubscriptionStatus.MailboxNotFound);
        var handler = new ImapSubscriptionCommandHandler(store, "#Public");

        var missing = await handler.HandleAsync(10, "A001", "UNSUBSCRIBE", "Missing", CancellationToken.None);
        var bad = await handler.HandleAsync(10, "A002", "UNSUBSCRIBE", "One Two", CancellationToken.None);

        Assert.AreEqual("A001 NO That mailbox does not exist.\r\n", missing);
        Assert.AreEqual("A002 BAD Command requires 1 parameter.\r\n", bad);
    }

    [TestMethod]
    public async Task SubscriptionCommandsRejectMalformedArgumentsAndPersistenceFailure()
    {
        var store = new FakeStore(ImapMailboxSubscriptionStatus.Failed);
        var handler = new ImapSubscriptionCommandHandler(store, "#Public");

        var bad = await handler.HandleAsync(10, "A001", "SUBSCRIBE", string.Empty, CancellationToken.None);
        var failed = await handler.HandleAsync(10, "A002", "UNSUBSCRIBE", "Projects", CancellationToken.None);

        Assert.AreEqual("A001 BAD Command requires at least 1 parameter.\r\n", bad);
        Assert.AreEqual("A002 NO UNSUBSCRIBE failed\r\n", failed);
    }

    private sealed class FakeStore : IImapMailboxSubscriptionStore
    {
        public FakeStore(ImapMailboxSubscriptionStatus status)
        {
            Status = status;
        }

        public ImapMailboxSubscriptionStatus Status { get; set; }
        public int LastAccountId { get; private set; }
        public string? LastMailboxName { get; private set; }
        public bool LastSubscribed { get; private set; }

        public ValueTask<ImapMailboxSubscriptionResult> SetSubscribedAsync(
            int requesterAccountId,
            string mailboxName,
            bool subscribed,
            CancellationToken cancellationToken)
        {
            LastAccountId = requesterAccountId;
            LastMailboxName = mailboxName;
            LastSubscribed = subscribed;
            return ValueTask.FromResult(new ImapMailboxSubscriptionResult(Status));
        }
    }
}
