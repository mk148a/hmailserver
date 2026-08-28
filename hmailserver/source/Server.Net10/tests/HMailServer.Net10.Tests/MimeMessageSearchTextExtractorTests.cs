using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MimeKit;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class MimeMessageSearchTextExtractorTests
{
    [TestMethod]
    public void Extract_ReturnsMimeDecodedSubject()
    {
        using var stream = new MemoryStream(
            "Subject: =?utf-8?B?UXVhcnRlcmx5IHLDqXN1bcOp?=\r\n\r\nBody"u8.ToArray());
        var message = MimeMessage.Load(stream);

        var text = MimeMessageSearchTextExtractor.Extract(
            message,
            new MessageFileSearchDocumentSourceOptions(Path.GetTempPath()));

        Assert.AreEqual("Quarterly résumé", text.SubjectText);
    }

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

    [TestMethod]
    public void Extract_ReturnsUntruncatedDecodedFileSearchDomains()
    {
        using var stream = new MemoryStream((
            "X-Description: =?utf-8?Q?Quarterly_r=C3=A9sum=C3=A9?=\r\n" +
            "Content-Type: multipart/alternative; boundary=part\r\n" +
            "\r\n" +
            "--part\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            "Content-Transfer-Encoding: quoted-printable\r\n" +
            "\r\n" +
            "decoded plain body tail-marker\r\n" +
            "--part\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n" +
            "Content-Transfer-Encoding: quoted-printable\r\n" +
            "\r\n" +
            "<p data-marker=3D\"needle\">decoded html</p>\r\n" +
            "--part--\r\n").Select(character => (byte)character).ToArray());
        var message = MimeMessage.Load(stream);

        var text = MimeMessageSearchTextExtractor.Extract(
            message,
            new MessageFileSearchDocumentSourceOptions(
                Path.GetTempPath(),
                MaxHeaderChars: 10,
                MaxBodyChars: 10,
                MaxCombinedChars: 10));

        StringAssert.Contains(text.FileSearchHeaderText, "X-Description: Quarterly résumé");
        StringAssert.Contains(text.FileSearchPlainBodyText, "tail-marker");
        StringAssert.Contains(text.FileSearchHtmlBodyText, "data-marker=\"needle\"");
    }

    [TestMethod]
    public void Extract_FileSearchDomainsExcludeTextAttachment()
    {
        using var stream = new MemoryStream((
            "Subject: Attachment exclusion\r\n" +
            "Content-Type: multipart/mixed; boundary=part\r\n" +
            "\r\n" +
            "--part\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            "\r\n" +
            "visible body\r\n" +
            "--part\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            "Content-Disposition: attachment; filename=notes.txt\r\n" +
            "\r\n" +
            "attachment-only-needle\r\n" +
            "--part--\r\n").Select(character => (byte)character).ToArray());
        var message = MimeMessage.Load(stream);

        var text = MimeMessageSearchTextExtractor.Extract(
            message,
            new MessageFileSearchDocumentSourceOptions(Path.GetTempPath()));

        StringAssert.Contains(text.FileSearchPlainBodyText, "visible body");
        Assert.IsFalse(text.FileSearchHeaderText.Contains("attachment-only-needle", StringComparison.Ordinal));
        Assert.IsFalse(text.FileSearchPlainBodyText.Contains("attachment-only-needle", StringComparison.Ordinal));
        Assert.IsFalse(text.FileSearchHtmlBodyText.Contains("attachment-only-needle", StringComparison.Ordinal));
    }
}
