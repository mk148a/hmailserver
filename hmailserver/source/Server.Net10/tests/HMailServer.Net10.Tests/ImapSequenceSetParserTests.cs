using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapSequenceSetParserTests
{
    [TestMethod]
    public void Parse_NormalizesRangesAndOpenEndedStar()
    {
        var ranges = ImapSequenceSetParser.Parse(
            "10:1,15,*:20",
            "FETCH",
            "FETCH",
            "FETCH message set",
            static message => new ImapFetchParseException(message));

        CollectionAssert.AreEqual(
            new[]
            {
                new ImapIdRange(1, 10),
                new ImapIdRange(15, 15),
                new ImapIdRange(20, null)
            },
            ranges.ToArray());
    }

    [TestMethod]
    public void Parse_RejectsBareStarWithoutHighWaterMark()
    {
        var exception = Assert.ThrowsExactly<ImapFetchParseException>(
            () => ImapSequenceSetParser.Parse(
                "*",
                "FETCH",
                "FETCH",
                "FETCH message set",
                static message => new ImapFetchParseException(message)));

        Assert.AreEqual("Bare '*' FETCH requires mailbox high-water mark context.", exception.Message);
    }
}
