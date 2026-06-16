using HMailServer.Core.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapAclRightsTests
{
    [TestMethod]
    public void Format_UsesRfc4314Ordering()
    {
        Assert.AreEqual("lrswipkxtea", ImapAclRights.Format(ImapAclRights.All));
    }

    [TestMethod]
    public void TryParseChange_ParsesReplaceAddAndRemove()
    {
        Assert.IsTrue(ImapAclRights.TryParseChange("lr", out var replace));
        Assert.AreEqual(ImapAclRightsChangeMode.Replace, replace.Mode);
        Assert.AreEqual(ImapAclRights.Lookup | ImapAclRights.Read, replace.Rights);

        Assert.IsTrue(ImapAclRights.TryParseChange("+s", out var add));
        Assert.AreEqual(ImapAclRightsChangeMode.Add, add.Mode);
        Assert.AreEqual(ImapAclRights.WriteSeen, add.Rights);

        Assert.IsTrue(ImapAclRights.TryParseChange("-t", out var remove));
        Assert.AreEqual(ImapAclRightsChangeMode.Remove, remove.Mode);
        Assert.AreEqual(ImapAclRights.WriteDeleted, remove.Rights);
    }
}
