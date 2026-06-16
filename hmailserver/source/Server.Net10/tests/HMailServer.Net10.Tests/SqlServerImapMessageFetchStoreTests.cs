using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImapMessageFetchStoreTests
{
    [TestMethod]
    public void Plan_UsesUidRangesForUidFetch()
    {
        var plan = SqlServerImapMessageFetchStore.Plan(
            new ImapFetchRequest(
                AccountId: 10,
                FolderId: 20,
                MessageSet:
                [
                    new ImapIdRange(100, 200),
                    new ImapIdRange(333, 333)
                ],
                UseUid: true,
                Items: [ImapFetchDataItem.Flags]));

        StringAssert.Contains(plan.CommandText, "ROW_NUMBER() OVER (ORDER BY m.messageuid ASC)");
        StringAssert.Contains(plan.CommandText, "messageuid BETWEEN @RangeStart0 AND @RangeEnd0");
        StringAssert.Contains(plan.CommandText, "messageuid = @RangeStart1");
        Assert.AreEqual(10, plan.Parameters["@AccountId"]);
        Assert.AreEqual(20, plan.Parameters["@FolderId"]);
        Assert.AreEqual(100L, plan.Parameters["@RangeStart0"]);
        Assert.AreEqual(200L, plan.Parameters["@RangeEnd0"]);
        Assert.AreEqual(333L, plan.Parameters["@RangeStart1"]);
    }

    [TestMethod]
    public void Plan_UsesSequenceNumberRangesForNonUidFetch()
    {
        var plan = SqlServerImapMessageFetchStore.Plan(
            new ImapFetchRequest(
                AccountId: 10,
                FolderId: 20,
                MessageSet: [new ImapIdRange(1, null)],
                UseUid: false,
                Items: [ImapFetchDataItem.Rfc822Size]));

        StringAssert.Contains(plan.CommandText, "sequencenumber >= @RangeStart0");
        StringAssert.Contains(plan.CommandText, "ORDER BY messageuid ASC");
    }
}
