using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MimeKit;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class MimeMessageSearchTextExtractorTests
{
    [TestMethod]
    public void Extract_IncludesDecodedHeadersPlainTextAndHtmlText()
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.test"));
        message.To.Add(MailboxAddress.Parse("receiver@example.test"));
        message.Subject = "Quarterly report";
        message.Body = new BodyBuilder
        {
            TextBody = "plain body invoice",
            HtmlBody = "<html><body><p>html &amp; body</p></body></html>"
        }.ToMessageBody();

        var text = MimeMessageSearchTextExtractor.Extract(
            message,
            new MessageFileSearchDocumentSourceOptions(Path.GetTempPath()));

        StringAssert.Contains(text.HeaderText, "Subject: Quarterly report");
        StringAssert.Contains(text.BodyText, "plain body invoice");
        StringAssert.Contains(text.BodyText, "html & body");
        StringAssert.Contains(text.CombinedText, "sender@example.test");
    }

    [TestMethod]
    public void Extract_RespectsTextLimits()
    {
        var message = new MimeMessage();
        message.Subject = new string('s', 200);
        message.Body = new TextPart("plain")
        {
            Text = new string('b', 200)
        };

        var text = MimeMessageSearchTextExtractor.Extract(
            message,
            new MessageFileSearchDocumentSourceOptions(
                Path.GetTempPath(),
                MaxHeaderChars: 20,
                MaxBodyChars: 30,
                MaxCombinedChars: 40));

        Assert.IsTrue(text.HeaderText.Length <= 20);
        Assert.AreEqual(30, text.BodyText.Length);
        Assert.IsTrue(text.CombinedText.Length <= 40);
    }
}
