using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapIdleResponseFormatterTests
{
    [TestMethod]
    public void Format_FormatsMailboxCountEvents()
    {
        Assert.AreEqual(
            "* 12 EXISTS\r\n",
            ImapIdleResponseFormatter.Format(new ImapIdleEvent(ImapIdleEventKind.Exists, 12)));
        Assert.AreEqual(
            "* 2 RECENT\r\n",
            ImapIdleResponseFormatter.Format(new ImapIdleEvent(ImapIdleEventKind.Recent, 2)));
        Assert.AreEqual(
            "* 4 EXPUNGE\r\n",
            ImapIdleResponseFormatter.Format(new ImapIdleEvent(ImapIdleEventKind.Expunge, 4)));
    }

    [TestMethod]
    public void Format_FormatsFetchFlagsEvents()
    {
        var response = ImapIdleResponseFormatter.Format(
            new ImapIdleEvent(
                ImapIdleEventKind.FetchFlags,
                Number: 3,
                Flags: ImapMessageFlags.Seen | ImapMessageFlags.Flagged,
                Uid: 101));

        Assert.AreEqual("* 3 FETCH (FLAGS (\\Seen \\Flagged) UID 101)\r\n", response);
    }
}
