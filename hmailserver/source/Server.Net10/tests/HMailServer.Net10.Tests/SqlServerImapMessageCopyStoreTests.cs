using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImapMessageCopyStoreTests
{
    [TestMethod]
    public void PlanCopy_UsesUidOrSequenceRanges()
    {
        var plan = SqlServerImapMessageCopyStore.PlanCopy(
            new ImapCopyRequest(
                SourceAccountId: 77,
                SourceFolderId: 88,
                DestinationAccountId: 77,
                DestinationFolderId: 99,
                MessageSet: [new ImapIdRange(101, null)],
                UseUid: true,
                DeleteSource: false));

        StringAssert.Contains(plan.CommandText, "ROW_NUMBER() OVER (ORDER BY m.messageuid ASC)");
        StringAssert.Contains(plan.CommandText, "messageuid >= @RangeStart0");
        Assert.AreEqual(77, plan.Parameters["@SourceAccountId"]);
        Assert.AreEqual(88, plan.Parameters["@SourceFolderId"]);
        Assert.AreEqual(101L, plan.Parameters["@RangeStart0"]);

        var sequencePlan = SqlServerImapMessageCopyStore.PlanCopy(
            new ImapCopyRequest(
                SourceAccountId: 77,
                SourceFolderId: 88,
                DestinationAccountId: 77,
                DestinationFolderId: 99,
                MessageSet: [new ImapIdRange(1, 10)],
                UseUid: false,
                DeleteSource: true));

        StringAssert.Contains(sequencePlan.CommandText, "sequencenumber BETWEEN @RangeStart0 AND @RangeEnd0");
    }

    [TestMethod]
    public void CopySql_AllocatesDestinationUidAndQueuesIndexing()
    {
        StringAssert.Contains(SqlServerImapMessageCopyStore.AllocateUidSql, "SET foldercurrentuid = foldercurrentuid + 1");
        StringAssert.Contains(SqlServerImapMessageCopyStore.AllocateUidSql, "OUTPUT INSERTED.foldercurrentuid");
        StringAssert.Contains(SqlServerImapMessageCopyStore.InsertCopiedMessageSql, "OUTPUT INSERTED.messageid");
        StringAssert.Contains(SqlServerImapMessageCopyStore.InsertCopiedMessageSql, "messageuid");
        StringAssert.Contains(SqlServerImapMessageCopyStore.QueueCopiedMessageForIndexingSql, "hm_message_search_queue");
        StringAssert.Contains(SqlServerImapMessageCopyStore.DeleteSourceMessageSql, "DELETE FROM hm_messages");
    }
}
