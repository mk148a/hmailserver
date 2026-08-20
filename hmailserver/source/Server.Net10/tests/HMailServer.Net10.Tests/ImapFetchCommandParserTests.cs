using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapFetchCommandParserTests
{
    [TestMethod]
    public void Parse_MapsUidFetchItemsAndMessageSet()
    {
        var request = new ImapFetchCommandParser().Parse(
            accountId: 10,
            folderId: 20,
            arguments: "101:* (FLAGS RFC822.SIZE INTERNALDATE BODY.PEEK[])",
            useUid: true);

        Assert.AreEqual(10, request.AccountId);
        Assert.AreEqual(20, request.FolderId);
        Assert.IsTrue(request.UseUid);
        Assert.IsTrue(request.RequiresRawMessage);
        CollectionAssert.AreEqual(
            new[] { new ImapIdRange(101, null) },
            request.MessageSet.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                ImapFetchDataItem.Flags,
                ImapFetchDataItem.Rfc822Size,
                ImapFetchDataItem.InternalDate,
                ImapFetchDataItem.BodyPeek,
                ImapFetchDataItem.Uid
            },
            request.Items.ToArray());
    }

    [TestMethod]
    public void Parse_MapsFastMacroToMetadataItems()
    {
        var request = new ImapFetchCommandParser().Parse(
            accountId: 10,
            folderId: 20,
            arguments: "1:10 FAST",
            useUid: false);

        Assert.IsFalse(request.RequiresRawMessage);
        CollectionAssert.AreEqual(
            new[]
            {
                ImapFetchDataItem.Flags,
                ImapFetchDataItem.InternalDate,
                ImapFetchDataItem.Rfc822Size
            },
            request.Items.ToArray());
    }

    [TestMethod]
    public void Parse_MapsFullMacroToEnvelopeBodyStructureAndBody()
    {
        var request = new ImapFetchCommandParser().Parse(
            accountId: 10,
            folderId: 20,
            arguments: "1 FULL",
            useUid: false);

        Assert.IsTrue(request.RequiresRawMessage);
        CollectionAssert.AreEqual(
            new[]
            {
                ImapFetchDataItem.Flags,
                ImapFetchDataItem.InternalDate,
                ImapFetchDataItem.Rfc822Size,
                ImapFetchDataItem.Envelope,
                ImapFetchDataItem.BodyStructure,
                ImapFetchDataItem.Body
            },
            request.Items.ToArray());
    }

    [TestMethod]
    public void Parse_RejectsUnsupportedBodyPartial()
    {
        Assert.ThrowsExactly<ImapFetchParseException>(
            () => new ImapFetchCommandParser().Parse(
                accountId: 10,
                folderId: 20,
                arguments: "1 BODY[]<0.1024>",
                useUid: false));
    }
}
