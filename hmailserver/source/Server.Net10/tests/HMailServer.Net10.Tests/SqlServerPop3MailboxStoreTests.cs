using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerPop3MailboxStoreTests
{
    [TestMethod]
    public void SelectInboxMessagesSql_UsesLegacyInboxAndUidOrdering()
    {
        var sql = SqlServerPop3MailboxStore.SelectInboxMessagesSql;

        StringAssert.Contains(sql, "INNER JOIN hm_imapfolders AS f");
        StringAssert.Contains(sql, "f.folderparentid = -1");
        StringAssert.Contains(sql, "f.foldername = N'Inbox'");
        StringAssert.Contains(sql, "m.messageaccountid = @AccountId");
        StringAssert.Contains(sql, "m.messagetype = 2");
        StringAssert.Contains(sql, "ORDER BY m.messageuid ASC");
    }

    [TestMethod]
    public void SelectMessageFileSql_RestrictsRetrievalToAccountInbox()
    {
        var sql = SqlServerPop3MailboxStore.SelectMessageFileSql;

        StringAssert.Contains(sql, "SELECT TOP (1)");
        StringAssert.Contains(sql, "m.messageid = @MessageId");
        StringAssert.Contains(sql, "m.messageaccountid = @AccountId");
        StringAssert.Contains(sql, "f.folderparentid = -1");
        StringAssert.Contains(sql, "f.foldername = N'Inbox'");
        StringAssert.Contains(sql, "a.accountaddress");
    }

    [TestMethod]
    public void PlanSelectMessagesForDelete_UsesDistinctOrderedMessageIdParameters()
    {
        var plan = SqlServerPop3MailboxStore.PlanSelectMessagesForDelete(
            accountId: 10,
            messageIds: new long[] { 55, 44, 55 });

        StringAssert.Contains(plan.CommandText, "m.messageid IN (@MessageId0, @MessageId1");
        StringAssert.Contains(plan.CommandText, "ORDER BY m.messageuid ASC");
        Assert.AreEqual(10, plan.Parameters["@AccountId"]);
        Assert.AreEqual(44L, plan.Parameters["@MessageId0"]);
        Assert.AreEqual(55L, plan.Parameters["@MessageId1"]);
        Assert.AreEqual(3, plan.Parameters.Count);
    }

    [TestMethod]
    public void PlanDeleteMessages_RemovesSearchMetadataAndMailboxRowsForAccount()
    {
        var plan = SqlServerPop3MailboxStore.PlanDeleteMessages(
            accountId: 10,
            messageIds: new long[] { 44, 55 });

        StringAssert.Contains(plan.CommandText, "DELETE FROM hm_message_search_queue");
        StringAssert.Contains(plan.CommandText, "DELETE FROM hm_message_search_documents");
        StringAssert.Contains(plan.CommandText, "DELETE FROM hm_message_metadata");
        StringAssert.Contains(plan.CommandText, "DELETE FROM hm_messages");
        StringAssert.Contains(plan.CommandText, "messageaccountid = @AccountId");
        StringAssert.Contains(plan.CommandText, "messagetype = 2");
        StringAssert.Contains(plan.CommandText, "messageid IN (@MessageId0, @MessageId1");
        Assert.AreEqual(44L, plan.Parameters["@MessageId0"]);
        Assert.AreEqual(55L, plan.Parameters["@MessageId1"]);
    }

    [TestMethod]
    public async Task DeleteMessages_HoldsWriterAdmissionAndReleasesItOnCancellation()
    {
        var admission = new RecordingWriterAdmission();
        var store = new SqlServerPop3MailboxStore(
            new SqlServerConnectionFactory("Server=localhost,1;Database=unused;Integrated Security=true;Connect Timeout=1"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            admission.EnterAsync);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.DeleteMessagesAsync(
                new ImapAuthenticatedAccount(42, "user@example.test"),
                new long[] { 100 },
                new CancellationToken(canceled: true)));

        Assert.IsTrue(admission.WasEntered);
        Assert.IsTrue(admission.WasReleased);
        Assert.IsFalse(admission.IsHeld);
    }

    private sealed class RecordingWriterAdmission
    {
        public bool WasEntered { get; private set; }
        public bool WasReleased { get; private set; }
        public bool IsHeld => WasEntered && !WasReleased;

        public ValueTask<IDisposable> EnterAsync(CancellationToken cancellationToken)
        {
            WasEntered = true;
            return ValueTask.FromResult<IDisposable>(new Lease(this));
        }

        private sealed class Lease(RecordingWriterAdmission owner) : IDisposable
        {
            public void Dispose() => owner.WasReleased = true;
        }
    }
}
