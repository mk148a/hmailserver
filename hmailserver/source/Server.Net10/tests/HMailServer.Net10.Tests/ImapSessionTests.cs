using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapSessionTests
{
    [TestMethod]
    public async Task RunAsync_DispatchesSubscriptionCommandsOnlyForAuthenticatedContext()
    {
        var store = new CapturingSubscriptionStore();
        await using var stream = new DuplexMemoryStream(
            "A001 SUBSCRIBE Projects\r\nA002 UNSUBSCRIBE Projects\r\nA003 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            subscriptionStore: store);

        await session.RunAsync(
            stream,
            new ImapSessionContext(AccountId: 10),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "A001 OK Subscribe completed\r\n");
        StringAssert.Contains(output, "A002 OK Unsubscribe completed\r\n");
        Assert.AreEqual(2, store.CallCount);

        await using var unauthenticatedStream = new DuplexMemoryStream("A001 SUBSCRIBE Projects\r\n");
        await session.RunAsync(
            unauthenticatedStream,
            new ImapSessionContext(),
            CancellationToken.None);
        StringAssert.Contains(unauthenticatedStream.GetOutputText(), "A001 NO Authenticate first\r\n");
        Assert.AreEqual(2, store.CallCount);
    }

    [TestMethod]
    public async Task RunAsync_RejectsSelectedDeletedSubtreeAfterSameAccountFolderChange()
    {
        var tracker = new ImapFolderChangeTracker();
        var searchIndex = new CapturingSearchIndex(Array.Empty<MessageIdentity>());
        await using var stream = new DuplexMemoryStream(
            "A001 UID SEARCH ALL\r\nA002 LOGOUT\r\n",
            () => tracker.PublishDeletion(10, new[] { 20, 21 }));
        var session = CreateSession(
            searchIndex,
            folderChangeTracker: tracker);

        await session.RunAsync(
            stream,
            new ImapSessionContext(AccountId: 10, FolderId: 20),
            CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A001 NO Select a mailbox first\r\n");
        Assert.IsNull(searchIndex.LastRequest);
    }

    [TestMethod]
    public async Task RunAsync_IgnoresFolderChangesForOtherAccounts()
    {
        var tracker = new ImapFolderChangeTracker();
        var searchIndex = new CapturingSearchIndex(Array.Empty<MessageIdentity>());
        await using var stream = new DuplexMemoryStream(
            "A001 UID SEARCH ALL\r\nA002 LOGOUT\r\n",
            () => tracker.PublishDeletion(200, new[] { 20 }));
        var session = CreateSession(
            searchIndex,
            folderChangeTracker: tracker);

        await session.RunAsync(
            stream,
            new ImapSessionContext(AccountId: 100, FolderId: 20),
            CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A001 OK SEARCH completed\r\n");
        Assert.IsNotNull(searchIndex.LastRequest);
    }

    [TestMethod]
    public async Task RunAsync_ACLReadRevocationClearsSelectedMailboxBeforeStore()
    {
        var tracker = new ImapFolderChangeTracker();
        var mailboxStore = new AclRevalidatingMailboxStore(null);
        await using var stream = new DuplexMemoryStream(
            "A001 STORE 1 +FLAGS (\\Seen)\r\n",
            () => tracker.PublishAclChange(20));
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            mailboxStore: mailboxStore,
            mutationStore: new FakeMutationStore(),
            folderChangeTracker: tracker);

        await session.RunAsync(
            stream,
            new ImapSessionContext(AccountId: 100, FolderId: 20),
            CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A001 NO Select a mailbox first\r\n");
        Assert.AreEqual(1, mailboxStore.RevalidationCount);
    }

    [TestMethod]
    public async Task RunAsync_ACLWriteRevocationMakesSelectedMailboxReadOnly()
    {
        var tracker = new ImapFolderChangeTracker();
        var mailboxStore = new AclRevalidatingMailboxStore(
            new ImapMailboxSelection(0, 20, "#Public", 1, 0, 1, 2, null, IsReadOnly: true));
        await using var stream = new DuplexMemoryStream(
            "A001 STORE 1 +FLAGS (\\Seen)\r\n",
            () => tracker.PublishAclChange(20));
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            mailboxStore: mailboxStore,
            mutationStore: new FakeMutationStore(),
            folderChangeTracker: tracker);

        await session.RunAsync(
            stream,
            new ImapSessionContext(AccountId: 100, FolderId: 20),
            CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A001 NO Store command on read-only folder\r\n");
        Assert.AreEqual(1, mailboxStore.RevalidationCount);
    }

    [TestMethod]
    public async Task RunAsync_ExternalACLRevocationIsRejectedWithoutTrackerPublication()
    {
        var tracker = new ImapFolderChangeTracker();
        var mailboxStore = new AclRevalidatingMailboxStore(null);
        await using var stream = new DuplexMemoryStream(
            "A001 STORE 1 +FLAGS (\\Seen)\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            mailboxStore: mailboxStore,
            mutationStore: new FakeMutationStore(),
            folderChangeTracker: tracker);

        await session.RunAsync(
            stream,
            new ImapSessionContext(AccountId: 100, FolderId: 20),
            CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A001 NO Select a mailbox first\r\n");
        Assert.AreEqual(1, mailboxStore.RevalidationCount);
        Assert.AreEqual(0, tracker.GetAclGeneration(20));
    }

    [TestMethod]
    public async Task RunAsync_RefreshesSelectedMailboxNameAfterSameAccountRename()
    {
        var tracker = new ImapFolderChangeTracker();
        var idleNotifier = new CapturingIdleNotifier();
        await using var stream = new DuplexMemoryStream(
            "A001 IDLE\r\nDONE\r\nA002 LOGOUT\r\n",
            () => tracker.PublishUpsert(
                new ImapFolderAdministrationSnapshot(
                    20,
                    100,
                    -1,
                    "Renamed",
                    true,
                    1,
                    "2026-06-27 01:02:03")));
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            idleNotifier: idleNotifier,
            folderChangeTracker: tracker);

        await session.RunAsync(
            stream,
            new ImapSessionContext(AccountId: 100, FolderId: 20),
            CancellationToken.None);

        Assert.IsNotNull(idleNotifier.LastRequest);
        Assert.AreEqual("Renamed", idleNotifier.LastRequest.MailboxName);
    }

    [TestMethod]
    public async Task RunAsync_RefreshesSelectedPublicMailboxNameAfterPublicRename()
    {
        var tracker = new ImapFolderChangeTracker();
        var idleNotifier = new CapturingIdleNotifier();
        await using var stream = new DuplexMemoryStream(
            "A001 SELECT #Public\r\nA002 IDLE\r\nDONE\r\nA003 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            idleNotifier: idleNotifier,
            mailboxStore: new PublicMailboxStore(tracker),
            folderChangeTracker: tracker);

        await session.RunAsync(
            stream,
            new ImapSessionContext(AccountId: 100),
            CancellationToken.None);

        Assert.IsNotNull(idleNotifier.LastRequest);
        Assert.AreEqual("Renamed", idleNotifier.LastRequest.MailboxName);
    }

    [TestMethod]
    public void TryParse_ParsesUidSearchCommandLine()
    {
        var parsed = ImapCommandLine.TryParse(
            "A001 UID SEARCH TEXT \"invoice\"",
            out var commandLine);

        Assert.IsTrue(parsed);
        Assert.AreEqual("A001", commandLine.Tag);
        Assert.AreEqual("SEARCH", commandLine.Command);
        Assert.IsTrue(commandLine.IsUidCommand);
        Assert.AreEqual("TEXT \"invoice\"", commandLine.Arguments);
    }

    [TestMethod]
    public async Task RunAsync_DispatchesCapabilitySearchAndLogout()
    {
        var searchIndex = new CapturingSearchIndex(
        [
            new MessageIdentity(1, 10, 20, 101),
            new MessageIdentity(2, 10, 20, 105)
        ]);
        await using var stream = new DuplexMemoryStream(
            "A001 CAPABILITY\r\nA002 UID SEARCH TEXT \"invoice\" UNSEEN\r\nA003 LOGOUT\r\n");
        var session = CreateSession(searchIndex);

        await session.RunAsync(
            stream,
            new ImapSessionContext(AccountId: 10, FolderId: 20),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "* OK hMailServer .NET 10 IMAP ready\r\n");
        StringAssert.Contains(output, "* CAPABILITY IMAP4rev1 UIDPLUS SORT MOVE IDLE ACL QUOTA AUTH=PLAIN SASL-IR\r\nA001 OK CAPABILITY completed\r\n");
        StringAssert.Contains(output, "* SEARCH 101 105\r\nA002 OK SEARCH completed\r\n");
        StringAssert.Contains(output, "* BYE hMailServer IMAP session closing\r\nA003 OK LOGOUT completed\r\n");
        Assert.IsNotNull(searchIndex.LastRequest);
        Assert.IsTrue(searchIndex.LastRequest.ReturnUid);
        CollectionAssert.AreEqual(new[] { "invoice" }, searchIndex.LastRequest.GetAnyTerms().ToArray());
        Assert.AreEqual(ImapMessageFlags.Seen, searchIndex.LastRequest.ForbiddenFlags);
    }

    [TestMethod]
    public async Task RunAsync_LoginSelectAndSearchUsesSelectedMailbox()
    {
        var searchIndex = new CapturingSearchIndex(
        [
            new MessageIdentity(1, 77, 88, 101)
        ]);
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN \"user@example.test\" \"secret\"\r\nA002 SELECT \"INBOX\"\r\nA003 UID SEARCH TEXT \"invoice\"\r\nA004 LOGOUT\r\n");
        var session = CreateSession(
            searchIndex,
            new FakeAuthenticator(),
            new FakeMailboxStore());

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "A001 OK LOGIN completed\r\n");
        StringAssert.Contains(output, "* 9 EXISTS\r\n");
        StringAssert.Contains(output, "* OK [UIDNEXT 500] next uid\r\n");
        StringAssert.Contains(output, "A002 OK [READ-WRITE] SELECT completed\r\n");
        StringAssert.Contains(output, "* SEARCH 101\r\nA003 OK SEARCH completed\r\n");
        Assert.IsNotNull(searchIndex.LastRequest);
        Assert.AreEqual(77, searchIndex.LastRequest.AccountId);
        Assert.AreEqual(88, searchIndex.LastRequest.FolderId);
    }

    [TestMethod]
    public async Task RunAsync_AuthenticatePlainInitialResponseSelectsMailbox()
    {
        var token = EncodeSaslPlain(string.Empty, "user@example.test", "secret");
        await using var stream = new DuplexMemoryStream(
            $"A001 AUTHENTICATE PLAIN {token}\r\nA002 SELECT \"INBOX\"\r\nA003 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore());

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "A001 OK LOGIN completed\r\n");
        StringAssert.Contains(output, "A002 OK [READ-WRITE] SELECT completed\r\n");
    }

    [TestMethod]
    public async Task RunAsync_AuthenticatePlainContinuationSelectsMailbox()
    {
        var token = EncodeSaslPlain(string.Empty, "user@example.test", "secret");
        await using var stream = new DuplexMemoryStream(
            $"A001 AUTHENTICATE PLAIN\r\n{token}\r\nA002 SELECT \"INBOX\"\r\nA003 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore());

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "+ \r\n");
        StringAssert.Contains(output, "A001 OK LOGIN completed\r\n");
        StringAssert.Contains(output, "A002 OK [READ-WRITE] SELECT completed\r\n");
    }

    [TestMethod]
    public async Task RunAsync_AuthenticatePlainPassesAuthorizationIdentityToAuthenticator()
    {
        var token = EncodeSaslPlain("target@example.test", "master@example.test", "secret");
        await using var stream = new DuplexMemoryStream(
            $"A001 AUTHENTICATE PLAIN {token}\r\nA002 LOGOUT\r\n");
        var authenticator = new MasterAwareAuthenticator();
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            authenticator);

        await session.RunAsync(stream, new ImapSessionContext(), CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A001 OK LOGIN completed\r\n");
        Assert.AreEqual("master@example.test", authenticator.Username);
        Assert.AreEqual("target@example.test", authenticator.AuthorizationId);
        Assert.AreEqual("secret", authenticator.Password);
    }

    [TestMethod]
    public async Task RunAsync_ReportsMasterUserProtocolFailuresAsBadWithoutAutoBan()
    {
        var token = EncodeSaslPlain("target@example.test", "wrong@example.test", "secret");
        await using var stream = new DuplexMemoryStream(
            $"A001 AUTHENTICATE PLAIN {token}\r\n");
        var authenticator = new MasterAwareAuthenticator(protocolFailure: true);
        var recorder = new CapturingAutoBanRecorder(disconnect: true);
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            authenticator,
            autoBanLogonFailureRecorder: recorder);

        await session.RunAsync(
            stream,
            new ImapSessionContext(ClientIPAddress: "203.0.113.50"),
            CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A001 BAD Invalid master user.\r\n");
        Assert.AreEqual(0, recorder.Failures.Count);
    }

    [TestMethod]
    public async Task RunAsync_RunsOnClientLogonAfterSuccessfulLogin()
    {
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN \"user@example.test\" \"secret\"\r\nA002 LOGOUT\r\n");
        var eventExecutor = new CapturingSmtpEventScriptExecutor();
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            eventScriptExecutor: eventExecutor);

        await session.RunAsync(
            stream,
            new ImapSessionContext(
                IsSecureConnection: true,
                ClientIPAddress: "203.0.113.10",
                ClientPort: 14301,
                SessionId: 99),
            CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A001 OK LOGIN completed\r\n");
        var request = eventExecutor.Requests.Single();
        Assert.AreEqual("OnClientLogon", request.EventName);
        Assert.AreEqual(SmtpEventScriptArgumentShape.ClientOnly, request.ArgumentShape);
        Assert.AreEqual("user@example.test", request.Client.Username);
        Assert.AreEqual("203.0.113.10", request.Client.IPAddress);
        Assert.AreEqual(14301, request.Client.Port);
        Assert.AreEqual(99, request.Client.SessionId);
        Assert.IsTrue(request.Client.IsAuthenticated);
        Assert.IsTrue(request.Client.IsEncryptedConnection);
    }

    [TestMethod]
    public async Task RunAsync_RunsOnClientLogonAfterFailedLogin()
    {
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN \"user@example.test\" \"wrong\"\r\nA002 LOGOUT\r\n");
        var eventExecutor = new CapturingSmtpEventScriptExecutor();
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            eventScriptExecutor: eventExecutor);

        await session.RunAsync(
            stream,
            new ImapSessionContext(
                ClientIPAddress: "203.0.113.11",
                ClientPort: 14302,
                SessionId: 100),
            CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A001 NO Invalid user name or password.\r\n");
        var request = eventExecutor.Requests.Single();
        Assert.AreEqual("OnClientLogon", request.EventName);
        Assert.AreEqual("user@example.test", request.Client.Username);
        Assert.AreEqual("203.0.113.11", request.Client.IPAddress);
        Assert.AreEqual(14302, request.Client.Port);
        Assert.AreEqual(100, request.Client.SessionId);
        Assert.IsFalse(request.Client.IsAuthenticated);
        Assert.IsFalse(request.Client.IsEncryptedConnection);
    }

    [TestMethod]
    public async Task RunAsync_RecordsAutoBanFailureAndDisconnectsWhenThresholdReached()
    {
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN \"user@example.test\" \"wrong\"\r\nA002 CAPABILITY\r\n");
        var autoBanRecorder = new CapturingAutoBanRecorder(disconnect: true);
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            eventScriptExecutor: null,
            autoBanLogonFailureRecorder: autoBanRecorder);

        await session.RunAsync(
            stream,
            new ImapSessionContext(
                ClientIPAddress: "203.0.113.12",
                ClientPort: 14303,
                SessionId: 101),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "A001 NO Invalid user name or password.\r\n");
        Assert.IsFalse(output.Contains("A002", StringComparison.Ordinal));
        var failure = autoBanRecorder.Failures.Single();
        Assert.AreEqual(IPAddress.Parse("203.0.113.12"), failure.ClientAddress);
        Assert.AreEqual("user@example.test", failure.Username);
    }

    [TestMethod]
    public async Task RunAsync_AuthenticatePlainAcceptsLegacyTabDelimitedToken()
    {
        var token = EncodeSaslPlain(string.Empty, "user@example.test", "secret", '\t');
        await using var stream = new DuplexMemoryStream(
            $"A001 AUTHENTICATE PLAIN {token}\r\nA002 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore());

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A001 OK LOGIN completed\r\n");
    }

    [TestMethod]
    public async Task RunAsync_AuthenticatePlainRejectsMalformedBase64()
    {
        await using var stream = new DuplexMemoryStream(
            "A001 AUTHENTICATE PLAIN not-base64!\r\nA002 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore());

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A001 BAD Command has malformed base64 token.\r\n");
    }

    [TestMethod]
    public async Task RunAsync_LoginForwardsEmptyPasswordToAuthenticationBoundary()
    {
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN user@example.test \"\"\r\n");
        var authenticator = new CapturingAuthenticator();
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            authenticator);

        await session.RunAsync(stream, new ImapSessionContext(), CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A001 NO Invalid user name or password.\r\n");
        Assert.AreEqual(1, authenticator.Calls);
        Assert.AreEqual(string.Empty, authenticator.Password);
    }

    [TestMethod]
    public async Task RunAsync_AuthenticatePlainRejectsEmptyPasswordBeforeAuthenticator()
    {
        var token = EncodeSaslPlain(string.Empty, "user@example.test", string.Empty);
        await using var stream = new DuplexMemoryStream(
            $"A001 AUTHENTICATE PLAIN {token}\r\n");
        var authenticator = new CapturingAuthenticator();
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            authenticator);

        await session.RunAsync(stream, new ImapSessionContext(), CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A001 BAD Command is missing password.\r\n");
        Assert.AreEqual(0, authenticator.Calls);
    }

    [TestMethod]
    public async Task RunAsync_CapabilityOmitsPlainAuthWhenTlsIsRequired()
    {
        await using var stream = new DuplexMemoryStream("A001 CAPABILITY\r\nA002 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore(),
            options: new ImapSessionOptions { RequireTlsForAuthentication = true });

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "* CAPABILITY IMAP4rev1 UIDPLUS SORT MOVE IDLE ACL QUOTA\r\nA001 OK CAPABILITY completed\r\n");
    }

    [TestMethod]
    public async Task RunAsync_LoginRejectsClearConnectionWhenTlsIsRequired()
    {
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN user@example.test secret\r\nA002 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore(),
            options: new ImapSessionOptions { RequireTlsForAuthentication = true });

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A001 BAD A SSL/TLS-connection is required for authentication.\r\n");
    }

    [TestMethod]
    public async Task RunAsync_AuthenticatePlainAllowsSecureConnectionWhenTlsIsRequired()
    {
        var token = EncodeSaslPlain(string.Empty, "user@example.test", "secret");
        await using var stream = new DuplexMemoryStream(
            $"A001 AUTHENTICATE PLAIN {token}\r\nA002 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore(),
            options: new ImapSessionOptions { RequireTlsForAuthentication = true });

        await session.RunAsync(
            stream,
            new ImapSessionContext(IsSecureConnection: true),
            CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A001 OK LOGIN completed\r\n");
    }

    [TestMethod]
    public async Task RunAsync_LoginSelectAndSearchRecentUsesSessionSnapshot()
    {
        var searchIndex = new CapturingSearchIndex(
        [
            new MessageIdentity(1, 77, 88, 101)
        ]);
        var recentStore = new FakeRecentFlagStore([101]);
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN \"user@example.test\" \"secret\"\r\nA002 SELECT \"INBOX\"\r\nA003 UID SEARCH RECENT\r\nA004 LOGOUT\r\n");
        var session = CreateSession(
            searchIndex,
            new FakeAuthenticator(),
            new FakeMailboxStore(),
            recentFlagStore: recentStore);

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "* 1 RECENT\r\n");
        StringAssert.Contains(output, "* SEARCH 101\r\nA003 OK SEARCH completed\r\n");
        Assert.IsTrue(recentStore.ClearRecentFlags);
        Assert.AreEqual(77, recentStore.AccountId);
        Assert.AreEqual(88, recentStore.FolderId);
        Assert.IsNotNull(searchIndex.LastRequest);
        CollectionAssert.AreEqual(new[] { 101L }, searchIndex.LastRequest.SessionRecentUids!.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_LoginSelectAndUidFetchUsesSelectedMailbox()
    {
        var fetchStore = new CapturingFetchStore(
        [
            new ImapFetchedMessage(
                new MessageIdentity(1, 77, 88, 101),
                SequenceNumber: 1,
                Flags: ImapMessageFlags.Seen,
                SizeBytes: 12,
                InternalDateUtc: new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero),
                RawMessage: null)
        ]);
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN \"user@example.test\" \"secret\"\r\nA002 SELECT \"INBOX\"\r\nA003 UID FETCH 101 (FLAGS UID RFC822.SIZE)\r\nA004 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore(),
            fetchStore);

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "* 1 FETCH (FLAGS (\\Seen) UID 101 RFC822.SIZE 12)\r\nA003 OK FETCH completed\r\n");
        Assert.IsNotNull(fetchStore.LastRequest);
        Assert.AreEqual(77, fetchStore.LastRequest.AccountId);
        Assert.AreEqual(88, fetchStore.LastRequest.FolderId);
        Assert.IsTrue(fetchStore.LastRequest.UseUid);
    }

    [TestMethod]
    public async Task RunAsync_LoginSelectAndUidFetchEnvelopeBodyStructure()
    {
        var rawMessage = Encoding.UTF8.GetBytes(
            "Subject: Invoice\r\n" +
            "From: Sender <sender@example.test>\r\n" +
            "To: User <user@example.test>\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            "\r\n" +
            "Hello\r\n");
        var fetchStore = new CapturingFetchStore(
        [
            new ImapFetchedMessage(
                new MessageIdentity(1, 77, 88, 101),
                SequenceNumber: 1,
                Flags: 0,
                SizeBytes: rawMessage.Length,
                InternalDateUtc: new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero),
                RawMessage: rawMessage)
        ]);
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN \"user@example.test\" \"secret\"\r\nA002 SELECT \"INBOX\"\r\nA003 UID FETCH 101 (ENVELOPE BODYSTRUCTURE)\r\nA004 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore(),
            fetchStore);

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "ENVELOPE");
        StringAssert.Contains(output, "\"Invoice\"");
        StringAssert.Contains(output, "BODYSTRUCTURE (\"TEXT\" \"PLAIN\"");
        StringAssert.Contains(output, "A003 OK FETCH completed\r\n");
        Assert.IsNotNull(fetchStore.LastRequest);
        Assert.IsTrue(fetchStore.LastRequest.RequiresRawMessage);
    }

    [TestMethod]
    public async Task RunAsync_LoginListStatusAndLogout()
    {
        var discoveryStore = new FakeDiscoveryStore();
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN \"user@example.test\" \"secret\"\r\nA002 LIST \"\" \"*\"\r\nA003 STATUS \"INBOX\" (MESSAGES UNSEEN UIDNEXT)\r\nA004 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore(),
            discoveryStore: discoveryStore);

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "* LIST (\\HasNoChildren) \".\" \"INBOX\"\r\n");
        StringAssert.Contains(output, "A002 OK LIST completed\r\n");
        StringAssert.Contains(output, "* STATUS \"INBOX\" (MESSAGES 9 UNSEEN 3 UIDNEXT 500)\r\n");
        StringAssert.Contains(output, "A003 OK STATUS completed\r\n");
        Assert.IsTrue(discoveryStore.ListWasCalled);
        Assert.IsTrue(discoveryStore.StatusWasCalled);
    }

    [TestMethod]
    public async Task RunAsync_LoginSelectUidStoreExpungeAndLogout()
    {
        var mutationStore = new FakeMutationStore();
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN \"user@example.test\" \"secret\"\r\nA002 SELECT \"INBOX\"\r\nA003 UID STORE 101 +FLAGS (\\Seen \\Deleted)\r\nA004 EXPUNGE\r\nA005 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore(),
            mutationStore: mutationStore);

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "* 1 FETCH (FLAGS (\\Deleted \\Seen) UID 101)\r\n");
        StringAssert.Contains(output, "A003 OK STORE completed\r\n");
        StringAssert.Contains(output, "* 1 EXPUNGE\r\nA004 OK EXPUNGE completed\r\n");
        Assert.IsNotNull(mutationStore.LastStoreRequest);
        Assert.AreEqual(77, mutationStore.LastStoreRequest.AccountId);
        Assert.AreEqual(88, mutationStore.LastStoreRequest.FolderId);
        Assert.IsTrue(mutationStore.LastStoreRequest.UseUid);
        Assert.AreEqual(77, mutationStore.LastExpungeAccountId);
        Assert.AreEqual(88, mutationStore.LastExpungeFolderId);
    }

    [TestMethod]
    public async Task RunAsync_LoginSelectUidCopyMoveAndLogout()
    {
        var copyStore = new FakeCopyStore();
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN \"user@example.test\" \"secret\"\r\nA002 SELECT \"INBOX\"\r\nA003 UID COPY 101 \"Archive\"\r\nA004 MOVE 1 \"Archive\"\r\nA005 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore(),
            copyStore: copyStore);

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "A003 OK COPY completed\r\n");
        StringAssert.Contains(output, "* 1 EXPUNGE\r\nA004 OK MOVE completed\r\n");
        Assert.AreEqual(2, copyStore.Requests.Count);
        Assert.IsTrue(copyStore.Requests[0].UseUid);
        Assert.IsFalse(copyStore.Requests[0].DeleteSource);
        Assert.IsFalse(copyStore.Requests[1].UseUid);
        Assert.IsTrue(copyStore.Requests[1].DeleteSource);
        Assert.AreEqual(77, copyStore.Requests[0].DestinationAccountId);
        Assert.AreEqual(99, copyStore.Requests[0].DestinationFolderId);
    }

    [TestMethod]
    public async Task RunAsync_LoginSelectUidSortAndLogout()
    {
        var sortIndex = new CapturingSortIndex(
        [
            new MessageIdentity(2, 77, 88, 105),
            new MessageIdentity(1, 77, 88, 101)
        ]);
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN \"user@example.test\" \"secret\"\r\nA002 SELECT \"INBOX\"\r\nA003 UID SORT (REVERSE DATE SUBJECT) UTF-8 UNSEEN TEXT \"invoice\"\r\nA004 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore(),
            sortIndex: sortIndex);

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "* SORT 105 101\r\nA003 OK SORT completed\r\n");
        Assert.IsNotNull(sortIndex.LastRequest);
        Assert.IsTrue(sortIndex.LastRequest.ReturnUid);
        Assert.AreEqual(77, sortIndex.LastRequest.SearchRequest.AccountId);
        Assert.AreEqual(88, sortIndex.LastRequest.SearchRequest.FolderId);
        Assert.AreEqual(ImapMessageFlags.Seen, sortIndex.LastRequest.SearchRequest.ForbiddenFlags);
        CollectionAssert.AreEqual(new[] { "invoice" }, sortIndex.LastRequest.SearchRequest.GetAnyTerms().ToArray());
        Assert.AreEqual(ImapSortKey.Date, sortIndex.LastRequest.Criteria[0].Key);
        Assert.IsTrue(sortIndex.LastRequest.Criteria[0].Descending);
        Assert.AreEqual(ImapSortKey.Subject, sortIndex.LastRequest.Criteria[1].Key);
        Assert.IsFalse(sortIndex.LastRequest.Criteria[1].Descending);
    }

    [TestMethod]
    public async Task RunAsync_LoginSelectIdleDoneAndLogout()
    {
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN \"user@example.test\" \"secret\"\r\nA002 SELECT \"INBOX\"\r\nA003 IDLE\r\nDONE\r\nA004 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore());

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "+ idling\r\n");
        StringAssert.Contains(output, "A003 OK IDLE completed\r\n");
        StringAssert.Contains(output, "* BYE hMailServer IMAP session closing\r\nA004 OK LOGOUT completed\r\n");
    }

    [TestMethod]
    public async Task RunAsync_LoginGetAclAndLogout()
    {
        var aclStore = new FakeAclStore();
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN \"user@example.test\" \"secret\"\r\nA002 GETACL \"#Public.Shared\"\r\nA003 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore(),
            aclStore: aclStore);

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "* ACL \"#Public.Shared\" user@example.test lrw Anyone l\r\n");
        StringAssert.Contains(output, "A002 OK GETACL completed\r\n");
        Assert.AreEqual(77, aclStore.LastRequesterAccountId);
        Assert.AreEqual("#Public.Shared", aclStore.LastMailboxName);
    }

    [TestMethod]
    public async Task RunAsync_LoginGetQuotaRootAndLogout()
    {
        var quotaStore = new FakeQuotaStore();
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN \"user@example.test\" \"secret\"\r\nA002 GETQUOTAROOT \"INBOX\"\r\nA003 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore(),
            quotaStore: quotaStore);

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "* QUOTAROOT \"INBOX\" \"\"\r\n* QUOTA \"\" (STORAGE 2048 10240)\r\n");
        StringAssert.Contains(output, "A002 OK GETQUOTAROOT completed\r\n");
        Assert.AreEqual(77, quotaStore.LastRequesterAccountId);
        Assert.AreEqual("INBOX", quotaStore.LastMailboxName);
    }

    [TestMethod]
    public async Task RunAsync_LoginAppendLiteralAndLogout()
    {
        var appendStore = new FakeAppendStore();
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN \"user@example.test\" \"secret\"\r\nA002 APPEND \"INBOX\" (\\Seen) {5}\r\nHello\r\nA003 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore(),
            appendStore: appendStore);

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "+ Ready for literal data\r\n");
        StringAssert.Contains(output, "A002 OK [APPENDUID 123 501] APPEND completed\r\n");
        Assert.IsNotNull(appendStore.LastRequest);
        Assert.AreEqual(77, appendStore.LastRequest.DestinationAccountId);
        Assert.AreEqual(88, appendStore.LastRequest.DestinationFolderId);
        Assert.AreEqual(ImapMessageFlags.Seen, appendStore.LastRequest.Flags);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("Hello"), appendStore.LastRequest.RawMessage);
    }

    [TestMethod]
    public async Task RunAsync_AppendToSelectedMailboxExtendsRecentSnapshot()
    {
        var searchIndex = new CapturingSearchIndex(
        [
            new MessageIdentity(10, 77, 88, 501)
        ]);
        var appendStore = new FakeAppendStore();
        var recentStore = new FakeRecentFlagStore([101]);
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN \"user@example.test\" \"secret\"\r\nA002 SELECT \"INBOX\"\r\nA003 APPEND \"INBOX\" {5}\r\nHello\r\nA004 UID SEARCH RECENT\r\nA005 LOGOUT\r\n");
        var session = CreateSession(
            searchIndex,
            new FakeAuthenticator(),
            new FakeMailboxStore(),
            appendStore: appendStore,
            recentFlagStore: recentStore);

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "A003 OK [APPENDUID 123 501] APPEND completed\r\n");
        StringAssert.Contains(output, "* SEARCH 501\r\nA004 OK SEARCH completed\r\n");
        Assert.IsNotNull(searchIndex.LastRequest);
        CollectionAssert.AreEquivalent(
            new[] { 101L, 501L },
            searchIndex.LastRequest.SessionRecentUids!.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_CopyToSelectedMailboxExtendsRecentSnapshot()
    {
        var searchIndex = new CapturingSearchIndex(
        [
            new MessageIdentity(2, 77, 88, 201)
        ]);
        var copyStore = new FakeCopyStore();
        var recentStore = new FakeRecentFlagStore([101]);
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN \"user@example.test\" \"secret\"\r\nA002 SELECT \"INBOX\"\r\nA003 UID COPY 101 \"INBOX\"\r\nA004 UID SEARCH RECENT\r\nA005 LOGOUT\r\n");
        var session = CreateSession(
            searchIndex,
            new FakeAuthenticator(),
            new FakeMailboxStore(),
            copyStore: copyStore,
            recentFlagStore: recentStore);

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "A003 OK COPY completed\r\n");
        StringAssert.Contains(output, "* SEARCH 201\r\nA004 OK SEARCH completed\r\n");
        Assert.IsNotNull(searchIndex.LastRequest);
        CollectionAssert.AreEquivalent(
            new[] { 101L, 201L },
            searchIndex.LastRequest.SessionRecentUids!.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_SearchBeforeSelectIsRejected()
    {
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN user@example.test secret\r\nA002 UID SEARCH TEXT \"invoice\"\r\nA003 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore());

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A002 NO Select a mailbox first\r\n");
    }

    [TestMethod]
    public async Task RunAsync_ExamineSelectsMailboxReadOnly()
    {
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN user@example.test secret\r\nA002 EXAMINE \"Projects.2026\"\r\nA003 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore());

        await session.RunAsync(
            stream,
            new ImapSessionContext(),
            CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A002 OK [READ-ONLY] EXAMINE completed\r\n");
    }

    [TestMethod]
    public async Task RunAsync_WritesBadForInvalidLineTerminator()
    {
        await using var stream = new DuplexMemoryStream("A001 NOOP\n");
        var session = CreateSession(new CapturingSearchIndex(Array.Empty<MessageIdentity>()));

        await session.RunAsync(
            stream,
            new ImapSessionContext(AccountId: 10, FolderId: 20),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "* BAD Protocol line ended without CRLF terminator.\r\n");
    }

    [TestMethod]
    public async Task RunAsync_UsesInjectedBoundaryWithImapCallerAndRemoteAddress()
    {
        var boundary = new CapturingClientAwareAuthenticationService(
            ImapAuthenticationResult.Success(new ImapAuthenticatedAccount(77, "user@example.test")));
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN user@example.test secret\r\nA002 LOGOUT\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore(),
            clientAwareAuthenticationService: boundary);

        await session.RunAsync(
            stream,
            new ImapSessionContext(ClientIPAddress: "203.0.113.31"),
            CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "A001 OK LOGIN completed\r\n");
        Assert.IsNotNull(boundary.LastRequest);
        Assert.AreEqual("user@example.test", boundary.LastRequest.Username);
        Assert.AreEqual("secret", boundary.LastRequest.Password);
        Assert.AreEqual(IPAddress.Parse("203.0.113.31"), boundary.LastRequest.ClientAddress);
        Assert.AreEqual(ClientAuthenticationCaller.Imap, boundary.LastRequest.Caller);
    }

    [TestMethod]
    public async Task RunAsync_UsesInjectedFailureAndDisconnectsForImap()
    {
        var boundary = new CapturingClientAwareAuthenticationService(
            ImapAuthenticationResult.Failure("Injected authentication failure."),
            disconnect: true);
        await using var stream = new DuplexMemoryStream(
            "A001 LOGIN user@example.test wrong\r\nA002 NOOP\r\n");
        var session = CreateSession(
            new CapturingSearchIndex(Array.Empty<MessageIdentity>()),
            new FakeAuthenticator(),
            new FakeMailboxStore(),
            clientAwareAuthenticationService: boundary);

        await session.RunAsync(
            stream,
            new ImapSessionContext(ClientIPAddress: "203.0.113.34"),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "A001 NO Injected authentication failure.\r\n");
        Assert.IsFalse(output.Contains("A001 OK LOGIN completed\r\n", StringComparison.Ordinal));
        Assert.IsFalse(output.Contains("A002 OK NOOP completed\r\n", StringComparison.Ordinal));
        Assert.IsNotNull(boundary.LastRequest);
        Assert.AreEqual(ClientAuthenticationCaller.Imap, boundary.LastRequest.Caller);
    }

    private static string EncodeSaslPlain(
        string authorizationId,
        string authenticationId,
        string password,
        char separator = '\0') =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
            string.Concat(authorizationId, separator, authenticationId, separator, password)));

    private static ImapSession CreateSession(
        CapturingSearchIndex searchIndex,
        IImapAccountAuthenticator? authenticator = null,
        IImapMailboxStore? mailboxStore = null,
        IImapMessageFetchStore? fetchStore = null,
        IImapMailboxDiscoveryStore? discoveryStore = null,
        IImapMessageMutationStore? mutationStore = null,
        IImapMessageCopyStore? copyStore = null,
        IImapMessageAppendStore? appendStore = null,
        IMessageSortIndex? sortIndex = null,
        IImapIdleNotifier? idleNotifier = null,
        IImapAclStore? aclStore = null,
        IImapQuotaStore? quotaStore = null,
        IImapMailboxSubscriptionStore? subscriptionStore = null,
        IImapRecentFlagStore? recentFlagStore = null,
        ImapSessionOptions? options = null,
        ISmtpEventScriptExecutor? eventScriptExecutor = null,
        IAutoBanLogonFailureRecorder? autoBanLogonFailureRecorder = null,
        IClientAwareAuthenticationService? clientAwareAuthenticationService = null,
        IImapFolderChangeTracker? folderChangeTracker = null)
    {
        var executor = new ImapSearchExecutor(searchIndex);
        var handler = new ImapSearchCommandHandler(new ImapSearchCommandParser(), executor);
        var sortHandler = sortIndex is null
            ? null
            : new ImapSortCommandHandler(
                new ImapSortCommandParser(new ImapSearchCommandParser()),
                new ImapSortExecutor(sortIndex, new SnapshotSequenceNumberResolver()));
        var fetchHandler = fetchStore is null
            ? null
            : new ImapFetchCommandHandler(new ImapFetchCommandParser(), fetchStore);
        var listHandler = discoveryStore is null
            ? null
            : new ImapListCommandHandler(discoveryStore, ".");
        var statusHandler = discoveryStore is null
            ? null
            : new ImapStatusCommandHandler(new ImapStatusCommandParser(), discoveryStore);
        var storeHandler = mutationStore is null
            ? null
            : new ImapStoreCommandHandler(new ImapStoreCommandParser(), mutationStore);
        var expungeHandler = mutationStore is null
            ? null
            : new ImapExpungeCommandHandler(mutationStore);
        var copyHandler = copyStore is null || mailboxStore is null
            ? null
            : new ImapCopyCommandHandler(new ImapCopyCommandParser(), mailboxStore, copyStore);
        var appendHandler = appendStore is null || mailboxStore is null
            ? null
            : new ImapAppendCommandHandler(new ImapAppendCommandParser(), mailboxStore, appendStore);
        var aclHandler = aclStore is null
            ? null
            : new ImapAclCommandHandler(aclStore);
        var quotaHandler = quotaStore is null
            ? null
            : new ImapQuotaCommandHandler(quotaStore);
        var subscriptionHandler = subscriptionStore is null
            ? null
            : new ImapSubscriptionCommandHandler(subscriptionStore, "#Public");
        return new ImapSession(
            handler,
            sortCommandHandler: sortHandler,
            fetchCommandHandler: fetchHandler,
            listCommandHandler: listHandler,
            statusCommandHandler: statusHandler,
            storeCommandHandler: storeHandler,
            expungeCommandHandler: expungeHandler,
            copyCommandHandler: copyHandler,
            appendCommandHandler: appendHandler,
            idleNotifier: idleNotifier,
            aclCommandHandler: aclHandler,
            quotaCommandHandler: quotaHandler,
            subscriptionCommandHandler: subscriptionHandler,
            recentFlagStore: recentFlagStore,
            options: options,
            accountAuthenticator: authenticator,
            mailboxStore: mailboxStore,
            eventScriptExecutor: eventScriptExecutor,
            autoBanLogonFailureRecorder: autoBanLogonFailureRecorder,
            clientAwareAuthenticationService: clientAwareAuthenticationService,
            folderChangeTracker: folderChangeTracker);
    }

    private sealed class CapturingSubscriptionStore : IImapMailboxSubscriptionStore
    {
        public int CallCount { get; private set; }

        public ValueTask<ImapMailboxSubscriptionResult> SetSubscribedAsync(
            int requesterAccountId,
            string mailboxName,
            bool subscribed,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult(ImapMailboxSubscriptionResult.Success());
        }
    }

    private sealed class CapturingIdleNotifier : IImapIdleNotifier
    {
        public ImapIdleWatchRequest? LastRequest { get; private set; }

        public async IAsyncEnumerable<ImapIdleEvent> WatchAsync(
            ImapIdleWatchRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastRequest = request;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            yield break;
        }
    }

    private sealed class CapturingSearchIndex : IMessageSearchIndex
    {
        private readonly IReadOnlyList<MessageIdentity> _identities;

        public CapturingSearchIndex(IReadOnlyList<MessageIdentity> identities)
        {
            _identities = identities;
        }

        public ImapSearchRequest? LastRequest { get; private set; }

        public ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);

        public ValueTask QueueForIndexingAsync(MessageIdentity identity, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask UpsertAsync(MessageSearchDocument document, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public async IAsyncEnumerable<MessageIdentity> SearchAsync(
            ImapSearchRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastRequest = request;
            await Task.Yield();
            foreach (var identity in _identities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return identity;
            }
        }
    }

    private sealed class CapturingSortIndex : IMessageSortIndex
    {
        private readonly IReadOnlyList<MessageIdentity> _identities;

        public CapturingSortIndex(IReadOnlyList<MessageIdentity> identities)
        {
            _identities = identities;
        }

        public ImapSortRequest? LastRequest { get; private set; }

        public async IAsyncEnumerable<MessageIdentity> SortAsync(
            ImapSortRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastRequest = request;
            await Task.Yield();
            foreach (var identity in _identities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return identity;
            }
        }
    }

    private sealed class SnapshotSequenceNumberResolver : IImapSequenceNumberResolver
    {
        public ValueTask<IReadOnlyDictionary<long, long>> ResolveMailboxSequenceNumbersAsync(
            int accountId,
            int folderId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyDictionary<long, long>>(
                new Dictionary<long, long>
                {
                    [1] = 1,
                    [2] = 2
                });
    }

    private sealed class FakeAuthenticator : IImapAccountAuthenticator
    {
        public ValueTask<ImapAuthenticationResult> AuthenticateAsync(
            string username,
            string password,
            CancellationToken cancellationToken)
        {
            if (username == "user@example.test" && password == "secret")
            {
                return ValueTask.FromResult(
                    ImapAuthenticationResult.Success(new ImapAuthenticatedAccount(77, username)));
            }

            return ValueTask.FromResult(ImapAuthenticationResult.Failure("Invalid user name or password."));
        }
    }

    private sealed class CapturingAuthenticator : IImapAccountAuthenticator
    {
        public int Calls { get; private set; }
        public string Password { get; private set; } = string.Empty;

        public ValueTask<ImapAuthenticationResult> AuthenticateAsync(
            string username,
            string password,
            CancellationToken cancellationToken)
        {
            Calls++;
            Password = password;
            return ValueTask.FromResult(ImapAuthenticationResult.Failure("Invalid user name or password."));
        }
    }

    private sealed class MasterAwareAuthenticator : IImapAccountAuthenticator
    {
        private readonly bool _protocolFailure;

        public MasterAwareAuthenticator(bool protocolFailure = false)
        {
            _protocolFailure = protocolFailure;
        }

        public string Username { get; private set; } = string.Empty;
        public string Password { get; private set; } = string.Empty;
        public string AuthorizationId { get; private set; } = string.Empty;

        public ValueTask<ImapAuthenticationResult> AuthenticateAsync(
            string username,
            string password,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(ImapAuthenticationResult.Failure("Unexpected ordinary authentication."));

        public ValueTask<ImapAuthenticationResult> AuthenticateAsync(
            string username,
            string password,
            string authorizationId,
            CancellationToken cancellationToken)
        {
            Username = username;
            Password = password;
            AuthorizationId = authorizationId;
            return ValueTask.FromResult(
                _protocolFailure
                    ? ImapAuthenticationResult.Failure("Invalid master user.", isProtocolError: true)
                    : ImapAuthenticationResult.Success(
                        new ImapAuthenticatedAccount(88, authorizationId)));
        }
    }

    private sealed class CapturingAutoBanRecorder : IAutoBanLogonFailureRecorder
    {
        private readonly bool _disconnect;

        public CapturingAutoBanRecorder(bool disconnect)
        {
            _disconnect = disconnect;
        }

        public List<(IPAddress ClientAddress, string Username)> Failures { get; } = [];

        public ValueTask<AutoBanLogonFailureResult> RecordFailureAsync(
            IPAddress clientAddress,
            string username,
            CancellationToken cancellationToken)
        {
            Failures.Add((clientAddress, username));
            return ValueTask.FromResult(
                new AutoBanLogonFailureResult(
                    Enabled: true,
                    FailureCount: Failures.Count,
                    Disconnect: _disconnect,
                    RangeCreated: _disconnect));
        }

        public ValueTask ClearOldFailuresAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class CapturingSmtpEventScriptExecutor : ISmtpEventScriptExecutor
    {
        private readonly List<SmtpEventScriptExecutionRequest> _requests = [];

        public IReadOnlyList<SmtpEventScriptExecutionRequest> Requests => _requests;

        public SmtpRuleScriptExecutionResult Execute(
            SmtpEventScriptExecutionRequest request,
            CancellationToken cancellationToken)
        {
            _requests.Add(request);
            return SmtpRuleScriptExecutionResult.Continue(request.MessageData);
        }
    }

    private sealed class FakeMailboxStore : IImapMailboxStore
    {
        public ValueTask<ImapMailboxSelection?> SelectMailboxAsync(
            int accountId,
            string mailboxName,
            bool readOnly,
            CancellationToken cancellationToken)
        {
            if (accountId == 77 &&
                (mailboxName.Equals("INBOX", StringComparison.OrdinalIgnoreCase) ||
                 mailboxName.Equals("Archive", StringComparison.OrdinalIgnoreCase) ||
                 mailboxName.Equals("Projects.2026", StringComparison.OrdinalIgnoreCase)))
            {
                return ValueTask.FromResult<ImapMailboxSelection?>(
                    new ImapMailboxSelection(
                        AccountId: accountId,
                        FolderId: mailboxName.Equals("Archive", StringComparison.OrdinalIgnoreCase) ? 99 : 88,
                        Name: mailboxName,
                        Exists: 9,
                        Recent: 1,
                        UidValidity: 123,
                        UidNext: 500,
                        FirstUnseenUid: 101,
                        IsReadOnly: readOnly));
            }

            return ValueTask.FromResult<ImapMailboxSelection?>(null);
        }
    }

    private sealed class PublicMailboxStore(IImapFolderChangeTracker changeTracker) : IImapMailboxStore
    {
        public ValueTask<ImapMailboxSelection?> SelectMailboxAsync(
            int accountId,
            string mailboxName,
            bool readOnly,
            CancellationToken cancellationToken)
        {
            changeTracker.PublishUpsert(
                new ImapFolderAdministrationSnapshot(
                    20,
                    0,
                    -1,
                    "Renamed",
                    true,
                    1,
                    "2026-08-01 00:00:00"));
            return ValueTask.FromResult<ImapMailboxSelection?>(
                new ImapMailboxSelection(
                    AccountId: 0,
                    FolderId: 20,
                    Name: "Old",
                    Exists: 1,
                    Recent: 0,
                    UidValidity: 1,
                    UidNext: 2,
                    FirstUnseenUid: null,
                    IsReadOnly: readOnly));
        }
    }

    private sealed class AclRevalidatingMailboxStore(ImapMailboxSelection? refreshedMailbox) :
        IImapMailboxStore,
        IImapSelectedMailboxAuthorization
    {
        public int RevalidationCount { get; private set; }

        public ValueTask<ImapMailboxSelection?> SelectMailboxAsync(
            int accountId,
            string mailboxName,
            bool readOnly,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<ImapMailboxSelection?>(
                new ImapMailboxSelection(
                    accountId,
                    20,
                    mailboxName,
                    1,
                    0,
                    1,
                    2,
                    null,
                    readOnly));

        public ValueTask<ImapMailboxSelection?> RevalidateSelectedMailboxAsync(
            int requesterAccountId,
            ImapMailboxSelection selectedMailbox,
            CancellationToken cancellationToken)
        {
            RevalidationCount++;
            return ValueTask.FromResult(refreshedMailbox);
        }
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

    private sealed class FakeDiscoveryStore : IImapMailboxDiscoveryStore
    {
        public bool ListWasCalled { get; private set; }

        public bool StatusWasCalled { get; private set; }

        public async IAsyncEnumerable<ImapMailboxListEntry> ListMailboxesAsync(
            int accountId,
            string referenceName,
            string mailboxPattern,
            bool subscribedOnly,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ListWasCalled = true;
            await Task.Yield();
            yield return new ImapMailboxListEntry(
                "INBOX",
                HasChildren: false,
                IsSelectable: true,
                IsSubscribed: true);
        }

        public ValueTask<ImapMailboxStatus?> GetStatusAsync(
            int accountId,
            string mailboxName,
            IReadOnlyList<ImapStatusItem> items,
            CancellationToken cancellationToken)
        {
            StatusWasCalled = true;
            return ValueTask.FromResult<ImapMailboxStatus?>(
                new ImapMailboxStatus(
                    mailboxName,
                    new Dictionary<ImapStatusItem, long>
                    {
                        [ImapStatusItem.Messages] = 9,
                        [ImapStatusItem.Unseen] = 3,
                        [ImapStatusItem.UidNext] = 500
                    }));
        }
    }

    private sealed class FakeMutationStore : IImapMessageMutationStore
    {
        public ImapStoreRequest? LastStoreRequest { get; private set; }

        public int LastExpungeAccountId { get; private set; }

        public int LastExpungeFolderId { get; private set; }

        public async IAsyncEnumerable<ImapStoredMessage> StoreFlagsAsync(
            ImapStoreRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastStoreRequest = request;
            await Task.Yield();
            yield return new ImapStoredMessage(
                new MessageIdentity(1, request.AccountId, request.FolderId, 101),
                SequenceNumber: 1,
                Flags: ImapMessageFlags.Seen | ImapMessageFlags.Deleted);
        }

        public async IAsyncEnumerable<ImapExpungedMessage> ExpungeDeletedAsync(
            int accountId,
            int folderId,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastExpungeAccountId = accountId;
            LastExpungeFolderId = folderId;
            await Task.Yield();
            yield return new ImapExpungedMessage(
                new MessageIdentity(1, accountId, folderId, 101),
                SequenceNumber: 1);
        }
    }

    private sealed class FakeCopyStore : IImapMessageCopyStore
    {
        public List<ImapCopyRequest> Requests { get; } = [];

        public async IAsyncEnumerable<ImapCopiedMessage> CopyAsync(
            ImapCopyRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Requests.Add(request);
            await Task.Yield();
            yield return new ImapCopiedMessage(
                new MessageIdentity(1, request.SourceAccountId, request.SourceFolderId, 101),
                1,
                new MessageIdentity(2, request.DestinationAccountId, request.DestinationFolderId, 201),
                request.DeleteSource ? 1 : null);
        }
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

    private sealed class FakeAclStore : IImapAclStore
    {
        public int LastRequesterAccountId { get; private set; }

        public string? LastMailboxName { get; private set; }

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
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ImapAclMutationResult(ImapAclCommandStatus.Success));

        public ValueTask<ImapAclMutationResult> DeleteAclAsync(
            int requesterAccountId,
            string mailboxName,
            string identifier,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ImapAclMutationResult(ImapAclCommandStatus.Success));
    }

    private sealed class FakeQuotaStore : IImapQuotaStore
    {
        public int LastRequesterAccountId { get; private set; }

        public string? LastMailboxName { get; private set; }

        public ValueTask<ImapQuotaResult> GetQuotaAsync(
            int requesterAccountId,
            string quotaRoot,
            CancellationToken cancellationToken)
        {
            LastRequesterAccountId = requesterAccountId;
            return ValueTask.FromResult(
                new ImapQuotaResult(
                    ImapQuotaCommandStatus.Success,
                    new ImapQuota(quotaRoot, UsedKilobytes: 2048, LimitKilobytes: 10240)));
        }

        public ValueTask<ImapQuotaRootResult> GetQuotaRootAsync(
            int requesterAccountId,
            string mailboxName,
            CancellationToken cancellationToken)
        {
            LastRequesterAccountId = requesterAccountId;
            LastMailboxName = mailboxName;
            return ValueTask.FromResult(
                new ImapQuotaRootResult(
                    ImapQuotaCommandStatus.Success,
                    mailboxName,
                    new ImapQuota(string.Empty, UsedKilobytes: 2048, LimitKilobytes: 10240)));
        }

        public ValueTask<ImapQuotaMutationResult> SetQuotaAsync(
            int requesterAccountId,
            string quotaRoot,
            long limitKilobytes,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ImapQuotaMutationResult(ImapQuotaCommandStatus.Success));
    }

    private sealed class FakeRecentFlagStore : IImapRecentFlagStore
    {
        private readonly IReadOnlyList<long> _recentUids;

        public FakeRecentFlagStore(IReadOnlyList<long> recentUids)
        {
            _recentUids = recentUids;
        }

        public int AccountId { get; private set; }

        public int FolderId { get; private set; }

        public bool ClearRecentFlags { get; private set; }

        public ValueTask<IReadOnlyList<long>> CaptureRecentUidsAsync(
            int accountId,
            int folderId,
            bool clearRecentFlags,
            CancellationToken cancellationToken)
        {
            AccountId = accountId;
            FolderId = folderId;
            ClearRecentFlags = clearRecentFlags;
            return ValueTask.FromResult(_recentUids);
        }
    }

    private sealed class DuplexMemoryStream : Stream
    {
        private readonly MemoryStream _input;
        private readonly MemoryStream _output = new();

        public DuplexMemoryStream(string input, Action? beforeFirstRead = null)
        {
            _input = new MemoryStream(Encoding.ASCII.GetBytes(input));
            _beforeFirstRead = beforeFirstRead;
        }

        private Action? _beforeFirstRead;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public string GetOutputText() => Encoding.ASCII.GetString(_output.ToArray());

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Interlocked.Exchange(ref _beforeFirstRead, null)?.Invoke();
            return _input.Read(buffer, offset, count);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Exchange(ref _beforeFirstRead, null)?.Invoke();
            return ValueTask.FromResult(_input.Read(buffer.Span));
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => _output.Write(buffer, offset, count);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _output.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _input.Dispose();
                _output.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
