using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapSearchCommandParserTests
{
    [TestMethod]
    public void ParseCriteria_MapsFastSearchCriteria()
    {
        var request = new ImapSearchCommandParser().ParseCriteria(
            accountId: 10,
            folderId: 20,
            criteriaText: "UID SEARCH CHARSET UTF-8 UID 100:200,333 SINCE 1-Jan-2026 BEFORE 3-Jan-2026 SENTSINCE 2-Jan-2026 SENTBEFORE 4-Jan-2026 LARGER 1024 SMALLER 4096 UNSEEN FLAGGED TEXT \"invoice\" BODY \"paid\" SUBJECT \"report\" HEADER \"X-Customer\" \"Ada\"",
            returnUid: false);

        Assert.IsTrue(request.ReturnUid);
        Assert.AreEqual(10, request.AccountId);
        Assert.AreEqual(20, request.FolderId);
        Assert.AreEqual(ImapMessageFlags.Flagged, request.RequiredFlags);
        Assert.AreEqual(ImapMessageFlags.Seen, request.ForbiddenFlags);
        Assert.AreEqual(new DateOnly(2026, 1, 1), request.Since);
        Assert.AreEqual(new DateOnly(2026, 1, 3), request.Before);
        Assert.AreEqual(new DateOnly(2026, 1, 2), request.SentSince);
        Assert.AreEqual(new DateOnly(2026, 1, 4), request.SentBefore);
        Assert.AreEqual(1024L, request.LargerThanBytes);
        Assert.AreEqual(4096L, request.SmallerThanBytes);
        CollectionAssert.AreEqual(
            new[]
            {
                new ImapIdRange(100, 200),
                new ImapIdRange(333, 333)
            },
            request.UidRanges.ToArray());
        CollectionAssert.AreEqual(
            Array.Empty<ImapIdRange>(),
            request.SequenceRanges.ToArray());
        CollectionAssert.AreEqual(
            new[] { "report", "X-Customer", "Ada" },
            request.GetHeaderTerms().ToArray());
        CollectionAssert.AreEqual(new[] { "paid" }, request.GetBodyTerms().ToArray());
        CollectionAssert.AreEqual(new[] { "invoice" }, request.GetAnyTerms().ToArray());
    }

    [TestMethod]
    public void ParseCriteria_MapsSentOnToSentDateRange()
    {
        var request = new ImapSearchCommandParser().ParseCriteria(
            accountId: 10,
            folderId: 20,
            criteriaText: "SEARCH SENTON 2-Jan-2026",
            returnUid: false);

        Assert.AreEqual(new DateOnly(2026, 1, 2), request.SentSince);
        Assert.AreEqual(new DateOnly(2026, 1, 3), request.SentBefore);
        Assert.IsNull(request.Since);
        Assert.IsNull(request.Before);
    }

    [TestMethod]
    public void ParseCriteria_MapsSequenceSetCriteria()
    {
        var request = new ImapSearchCommandParser().ParseCriteria(
            accountId: 10,
            folderId: 20,
            criteriaText: "SEARCH 1:3,7 UNSEEN",
            returnUid: false);

        Assert.IsFalse(request.ReturnUid);
        Assert.AreEqual(ImapMessageFlags.Seen, request.ForbiddenFlags);
        CollectionAssert.AreEqual(
            new[]
            {
                new ImapIdRange(1, 3),
                new ImapIdRange(7, 7)
            },
            request.SequenceRanges.ToArray());
    }

    [TestMethod]
    public void ParseCriteria_ThrowsOnUnsupportedKey()
    {
        Assert.ThrowsExactly<ImapSearchParseException>(
            () => new ImapSearchCommandParser().ParseCriteria(
                accountId: 1,
                folderId: 2,
                criteriaText: "OR FROM a@example.test TO b@example.test",
                returnUid: true));
    }

    [TestMethod]
    public void ParseCriteria_ThrowsOnBareUidStarWithoutMailboxContext()
    {
        Assert.ThrowsExactly<ImapSearchParseException>(
            () => new ImapSearchCommandParser().ParseCriteria(
                accountId: 1,
                folderId: 2,
                criteriaText: "UID *",
                returnUid: true));
    }
}
