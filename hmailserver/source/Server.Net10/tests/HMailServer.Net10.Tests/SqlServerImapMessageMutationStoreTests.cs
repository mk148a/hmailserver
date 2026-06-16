using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImapMessageMutationStoreTests
{
    [TestMethod]
    public void PlanStore_UsesUidOrSequenceRanges()
    {
        var plan = SqlServerImapMessageMutationStore.PlanStore(
            new ImapStoreRequest(
                AccountId: 10,
                FolderId: 20,
                MessageSet: [new ImapIdRange(101, null)],
                UseUid: true,
                Mode: ImapStoreMode.Add,
                Flags: ImapMessageFlags.Seen,
                Silent: false));

        StringAssert.Contains(plan.CommandText, "ROW_NUMBER() OVER (ORDER BY m.messageuid ASC)");
        StringAssert.Contains(plan.CommandText, "messageuid >= @RangeStart0");
        Assert.AreEqual(101L, plan.Parameters["@RangeStart0"]);

        var sequencePlan = SqlServerImapMessageMutationStore.PlanStore(
            new ImapStoreRequest(
                AccountId: 10,
                FolderId: 20,
                MessageSet: [new ImapIdRange(1, 10)],
                UseUid: false,
                Mode: ImapStoreMode.Remove,
                Flags: ImapMessageFlags.Deleted,
                Silent: true));

        StringAssert.Contains(sequencePlan.CommandText, "sequencenumber BETWEEN @RangeStart0 AND @RangeEnd0");
    }

    [TestMethod]
    public void ExpungeSql_DeletesMessagesAndSearchArtifacts()
    {
        var sql = SqlServerImapMessageMutationStore.BuildExpungeSnapshotSql();

        StringAssert.Contains(sql, "(messageflags & @DeletedFlag) = @DeletedFlag");
        StringAssert.Contains(SqlServerImapMessageMutationStore.DeleteMessageSql, "DELETE FROM hm_message_search_queue");
        StringAssert.Contains(SqlServerImapMessageMutationStore.DeleteMessageSql, "DELETE FROM hm_message_search_documents");
        StringAssert.Contains(SqlServerImapMessageMutationStore.DeleteMessageSql, "DELETE FROM hm_message_metadata");
        StringAssert.Contains(SqlServerImapMessageMutationStore.DeleteMessageSql, "DELETE FROM hm_messages");
    }
}
