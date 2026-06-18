using System.Text;
using HMailServer.Security;
using MimeKit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class MessageAttachmentPolicyTests
{
    [TestMethod]
    public async Task ApplyAsync_ReplacesBlockedAttachmentWithTextAttachment()
    {
        var messageData = CreateMessageWithAttachment("evil.exe", "MZ");
        var policy = new MimeMessageAttachmentPolicy(
            new MessageAttachmentPolicyOptions
            {
                Enabled = true,
                BlockedWildcards = ["*.exe"],
                ReplacementTextTemplate = "Blocked: %MACRO_FILE%"
            });

        var result = await policy.ApplyAsync(messageData, CancellationToken.None);

        Assert.IsTrue(result.Modified);
        CollectionAssert.AreEqual(new[] { "evil.exe" }, result.BlockedFileNames.ToArray());
        var attachment = (TextPart)GetSingleAttachment(result.MessageData);
        Assert.AreEqual("evil.exe.txt", attachment.FileName);
        Assert.AreEqual("Blocked: evil.exe", attachment.Text);
    }

    [TestMethod]
    public async Task ApplyAsync_PreservesMessageWhenNoWildcardMatches()
    {
        var messageData = CreateMessageWithAttachment("safe.txt", "hello");
        var policy = new MimeMessageAttachmentPolicy(
            new MessageAttachmentPolicyOptions
            {
                Enabled = true,
                BlockedWildcards = ["*.exe"]
            });

        var result = await policy.ApplyAsync(messageData, CancellationToken.None);

        Assert.IsFalse(result.Modified);
        CollectionAssert.AreEqual(messageData, result.MessageData);
    }

    private static byte[] CreateMessageWithAttachment(string fileName, string content)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.test"));
        message.To.Add(MailboxAddress.Parse("recipient@example.test"));
        message.Subject = "Attachment";

        var multipart = new Multipart("mixed")
        {
            new TextPart("plain") { Text = "Body" },
            new MimePart("application", "octet-stream")
            {
                FileName = fileName,
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                Content = new MimeContent(new MemoryStream(Encoding.ASCII.GetBytes(content)))
            }
        };
        message.Body = multipart;

        using var output = new MemoryStream();
        message.WriteTo(output);
        return output.ToArray();
    }

    private static MimeEntity GetSingleAttachment(byte[] messageData)
    {
        using var input = new MemoryStream(messageData, writable: false);
        var message = MimeMessage.Load(input);
        return message.Attachments.Single();
    }
}
