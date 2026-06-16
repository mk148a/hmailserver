using HMailServer.Core.Abstractions;
using HMailServer.Search.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImapSortPlannerTests
{
    [TestMethod]
    public void Plan_OrdersByMetadataFieldsAndStableUidTieBreaker()
    {
        var request = new ImapSortRequest(
            new ImapSearchRequest(
                AccountId: 10,
                FolderId: 20,
                MinUid: 100,
                MaxUid: 200,
                RequiredFlags: 1,
                ForbiddenFlags: 2,
                Since: new DateOnly(2026, 1, 1),
                Before: null,
                LargerThanBytes: null,
                SmallerThanBytes: null,
                HeaderText: null,
                BodyText: null,
                AnyText: null,
                ReturnUid: true),
            [
                new ImapSortCriterion(ImapSortKey.Date, Descending: true),
                new ImapSortCriterion(ImapSortKey.Subject, Descending: false)
            ]);

        var plan = new SqlServerImapSortPlanner().Plan(request);

        StringAssert.Contains(plan.CommandText, "LEFT JOIN hm_message_metadata AS md");
        StringAssert.Contains(plan.CommandText, "m.messageuid >= @MinUid");
        StringAssert.Contains(plan.CommandText, "ORDER BY COALESCE(md.metadata_dateutc, m.messagecreatetime) DESC, LOWER(COALESCE(md.metadata_subject, N'')) ASC, m.messageuid ASC;");
        Assert.AreEqual(10, plan.Parameters["@AccountId"]);
        Assert.AreEqual(100L, plan.Parameters["@MinUid"]);
    }

    [TestMethod]
    public void Plan_CombinesFullTextCandidatesWithSortOrder()
    {
        var searchRequest = new ImapSearchRequest(
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
            AnyTerms = ["invoice"]
        };
        var request = new ImapSortRequest(
            searchRequest,
            [new ImapSortCriterion(ImapSortKey.From, Descending: false)]);

        var plan = new SqlServerImapSortPlanner().Plan(request);

        StringAssert.Contains(plan.CommandText, "INNER JOIN hm_message_search_documents AS sd");
        StringAssert.Contains(plan.CommandText, "CONTAINS(sd.search_combined, @AnyText0)");
        StringAssert.Contains(plan.CommandText, "ORDER BY LOWER(COALESCE(md.metadata_from, N'')) ASC, m.messageuid ASC;");
        Assert.AreEqual("\"invoice\"", plan.Parameters["@AnyText0"]);
    }

    [TestMethod]
    public void Plan_UsesSessionRecentSnapshotBeforeSortOrder()
    {
        var searchRequest = new ImapSearchRequest(
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
        var request = new ImapSortRequest(
            searchRequest,
            [new ImapSortCriterion(ImapSortKey.Date, Descending: true)]);

        var plan = new SqlServerImapSortPlanner().Plan(request);

        StringAssert.Contains(plan.CommandText, "m.messageuid IN (@SessionRecentUid0, @SessionRecentUid1)");
        StringAssert.Contains(plan.CommandText, "ORDER BY COALESCE(md.metadata_dateutc, m.messagecreatetime) DESC, m.messageuid ASC;");
        Assert.IsFalse(plan.Parameters.ContainsKey("@RequiredFlags"));
    }
}
