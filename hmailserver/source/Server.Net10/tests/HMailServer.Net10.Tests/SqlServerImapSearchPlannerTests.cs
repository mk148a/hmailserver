using HMailServer.Core.Abstractions;
using HMailServer.Search.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImapSearchPlannerTests
{
    [TestMethod]
    public void Plan_UsesSqlPredicatesAndFullTextSearch()
    {
        var request = new ImapSearchRequest(
            AccountId: 10,
            FolderId: 20,
            MinUid: 100,
            MaxUid: 200,
            RequiredFlags: 1,
            ForbiddenFlags: 2,
            Since: new DateOnly(2026, 1, 1),
            Before: null,
            LargerThanBytes: 1024,
            SmallerThanBytes: null,
            HeaderText: "from@example.test",
            BodyText: null,
            AnyText: "invoice",
            ReturnUid: true)
        {
            SentSince = new DateOnly(2026, 1, 2),
            SentBefore = new DateOnly(2026, 1, 4)
        };

        var plan = new SqlServerImapSearchPlanner().Plan(request);

        StringAssert.Contains(plan.CommandText, "m.messageuid >= @MinUid");
        StringAssert.Contains(plan.CommandText, "LEFT JOIN hm_message_metadata AS md");
        StringAssert.Contains(plan.CommandText, "COALESCE(md.metadata_dateutc, m.messagecreatetime) >= @SentSince");
        StringAssert.Contains(plan.CommandText, "COALESCE(md.metadata_dateutc, m.messagecreatetime) < @SentBefore");
        StringAssert.Contains(plan.CommandText, "INNER JOIN hm_message_search_documents AS sd");
        StringAssert.Contains(plan.CommandText, "CONTAINS(sd.search_header, @HeaderText0)");
        StringAssert.Contains(plan.CommandText, "CONTAINS(sd.search_combined, @AnyText0)");
        Assert.AreEqual(10, plan.Parameters["@AccountId"]);
        Assert.AreEqual(new DateTime(2026, 1, 2), plan.Parameters["@SentSince"]);
        Assert.AreEqual(new DateTime(2026, 1, 4), plan.Parameters["@SentBefore"]);
        Assert.AreEqual("\"invoice\"", plan.Parameters["@AnyText0"]);
    }

    [TestMethod]
    public void Plan_UsesMultipleFullTextTermsAndUidRanges()
    {
        var request = new ImapSearchRequest(
            AccountId: 10,
            FolderId: 20,
            MinUid: null,
            MaxUid: null,
            RequiredFlags: null,
            ForbiddenFlags: null,
            Since: null,
            Before: null,
            LargerThanBytes: null,
            SmallerThanBytes: null,
            HeaderText: null,
            BodyText: null,
            AnyText: null,
            ReturnUid: true)
        {
            UidRanges =
            [
                new ImapIdRange(100, 200),
                new ImapIdRange(300, null)
            ],
            SequenceRanges =
            [
                new ImapIdRange(1, 10),
                new ImapIdRange(20, 20)
            ],
            HeaderTerms =
            [
                "from@example.test",
                "quarterly report"
            ],
            BodyTerms =
            [
                "paid"
            ],
            AnyTerms =
            [
                "invoice",
                "quote \"special\""
            ]
        };

        var plan = new SqlServerImapSearchPlanner().Plan(request);

        StringAssert.Contains(plan.CommandText, "m.messageuid BETWEEN @UidRangeStart0 AND @UidRangeEnd0");
        StringAssert.Contains(plan.CommandText, "m.messageuid >= @UidRangeStart1");
        StringAssert.Contains(plan.CommandText, "ROW_NUMBER() OVER (ORDER BY sm.messageuid ASC) AS sequencenumber");
        StringAssert.Contains(plan.CommandText, "sequenced.sequencenumber BETWEEN @SequenceRangeStart0 AND @SequenceRangeEnd0");
        StringAssert.Contains(plan.CommandText, "sequenced.sequencenumber = @SequenceRangeStart1");
        StringAssert.Contains(plan.CommandText, "CONTAINS(sd.search_header, @HeaderText0)");
        StringAssert.Contains(plan.CommandText, "CONTAINS(sd.search_header, @HeaderText1)");
        StringAssert.Contains(plan.CommandText, "CONTAINS(sd.search_body, @BodyText0)");
        StringAssert.Contains(plan.CommandText, "CONTAINS(sd.search_combined, @AnyText0)");
        StringAssert.Contains(plan.CommandText, "CONTAINS(sd.search_combined, @AnyText1)");
        Assert.AreEqual(100L, plan.Parameters["@UidRangeStart0"]);
        Assert.AreEqual(200L, plan.Parameters["@UidRangeEnd0"]);
        Assert.AreEqual(300L, plan.Parameters["@UidRangeStart1"]);
        Assert.AreEqual(1L, plan.Parameters["@SequenceRangeStart0"]);
        Assert.AreEqual(10L, plan.Parameters["@SequenceRangeEnd0"]);
        Assert.AreEqual(20L, plan.Parameters["@SequenceRangeStart1"]);
        Assert.AreEqual("\"quote \"\"special\"\"\"", plan.Parameters["@AnyText1"]);
    }

    [TestMethod]
    public void Plan_UsesSessionRecentSnapshotForRecentSearch()
    {
        var request = new ImapSearchRequest(
            AccountId: 10,
            FolderId: 20,
            MinUid: null,
            MaxUid: null,
            RequiredFlags: ImapMessageFlags.Recent,
            ForbiddenFlags: null,
            Since: null,
            Before: null,
            LargerThanBytes: null,
            SmallerThanBytes: null,
            HeaderText: null,
            BodyText: null,
            AnyText: null,
            ReturnUid: true)
        {
            SessionRecentUids = new HashSet<long> { 105, 101 }
        };

        var plan = new SqlServerImapSearchPlanner().Plan(request);

        StringAssert.Contains(plan.CommandText, "m.messageuid IN (@SessionRecentUid0, @SessionRecentUid1)");
        Assert.IsFalse(plan.Parameters.ContainsKey("@RequiredFlags"));
        Assert.AreEqual(101L, plan.Parameters["@SessionRecentUid0"]);
        Assert.AreEqual(105L, plan.Parameters["@SessionRecentUid1"]);
    }

    [TestMethod]
    public void Plan_UsesSessionRecentSnapshotForOldSearch()
    {
        var request = new ImapSearchRequest(
            AccountId: 10,
            FolderId: 20,
            MinUid: null,
            MaxUid: null,
            RequiredFlags: null,
            ForbiddenFlags: ImapMessageFlags.Recent,
            Since: null,
            Before: null,
            LargerThanBytes: null,
            SmallerThanBytes: null,
            HeaderText: null,
            BodyText: null,
            AnyText: null,
            ReturnUid: true)
        {
            SessionRecentUids = new HashSet<long> { 101 }
        };

        var plan = new SqlServerImapSearchPlanner().Plan(request);

        StringAssert.Contains(plan.CommandText, "m.messageuid NOT IN (@SessionRecentUid0)");
        Assert.IsFalse(plan.Parameters.ContainsKey("@ForbiddenFlags"));
        Assert.AreEqual(101L, plan.Parameters["@SessionRecentUid0"]);
    }
}
