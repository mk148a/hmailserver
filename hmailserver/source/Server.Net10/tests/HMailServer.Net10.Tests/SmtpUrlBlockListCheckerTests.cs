using System.Net;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MimeKit;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SmtpUrlBlockListCheckerTests
{
    [TestMethod]
    public async Task CheckAsync_QueriesUrlHostAndBlocksOnDnsHit()
    {
        var resolver = new FakeDnsAddressResolver(hostName =>
            hostName == "bad.example.multi.surbl.test"
                ? [IPAddress.Parse("127.0.0.2")]
                : []);
        var checker = new SmtpUrlBlockListChecker(
            resolver,
            new SmtpUrlBlockListOptions
            {
                Enabled = true,
                Zones = ["multi.surbl.test."],
                RejectionMessageTemplate = "Blocked URL {MatchedHost} by {ListHost}"
            });

        var result = await checker.CheckAsync(
            CreateRequest(CreateMessage("Visit http://bad.example/path")),
            CancellationToken.None);

        Assert.IsTrue(result.Listed);
        Assert.AreEqual("multi.surbl.test", result.ListHost);
        Assert.AreEqual("bad.example", result.MatchedHost);
        Assert.AreEqual("bad.example.multi.surbl.test", result.QueryHost);
        Assert.AreEqual("127.0.0.2", result.ResponseAddress);
        Assert.AreEqual("554 Blocked URL bad.example by multi.surbl.test", result.FailureResponse);
    }

    [TestMethod]
    public async Task CheckAsync_ChecksParentDomainsWithinBoundedCandidateLimit()
    {
        var resolver = new FakeDnsAddressResolver(hostName =>
            hostName == "bad.example.test.multi.surbl.test"
                ? [IPAddress.Parse("127.0.0.4")]
                : []);
        var checker = new SmtpUrlBlockListChecker(
            resolver,
            new SmtpUrlBlockListOptions
            {
                Enabled = true,
                Zones = ["multi.surbl.test"],
                MaxCandidateDomainsPerHost = 2
            });

        var result = await checker.CheckAsync(
            CreateRequest(CreateMessage("Visit https://www.bad.example.test/path")),
            CancellationToken.None);

        Assert.IsTrue(result.Listed);
        CollectionAssert.AreEqual(
            new[]
            {
                "www.bad.example.test.multi.surbl.test",
                "bad.example.test.multi.surbl.test"
            },
            resolver.Queries.ToArray());
    }

    [TestMethod]
    public async Task CheckAsync_SkipsWhenSpamScanningIsDisabled()
    {
        var resolver = new FakeDnsAddressResolver(_ => [IPAddress.Parse("127.0.0.2")]);
        var checker = new SmtpUrlBlockListChecker(
            resolver,
            new SmtpUrlBlockListOptions
            {
                Enabled = true,
                Zones = ["multi.surbl.test"]
            });

        var result = await checker.CheckAsync(
            CreateRequest(CreateMessage("Visit http://bad.example/")) with { EnableSpamScan = false },
            CancellationToken.None);

        Assert.IsFalse(result.Listed);
        Assert.AreEqual(0, resolver.Queries.Count);
    }

    [TestMethod]
    public async Task CheckAsync_FailsOpenWhenResolverThrows()
    {
        var resolver = new FakeDnsAddressResolver(_ => throw new IOException("dns failed"));
        var checker = new SmtpUrlBlockListChecker(
            resolver,
            new SmtpUrlBlockListOptions
            {
                Enabled = true,
                Zones = ["multi.surbl.test"]
            });

        var result = await checker.CheckAsync(
            CreateRequest(CreateMessage("Visit http://bad.example/")),
            CancellationToken.None);

        Assert.IsFalse(result.Listed);
        CollectionAssert.AreEqual(
            new[] { "bad.example.multi.surbl.test" },
            resolver.Queries.ToArray());
    }

    private static byte[] CreateMessage(string body)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.test"));
        message.To.Add(MailboxAddress.Parse("recipient@example.test"));
        message.Subject = "URL";
        message.Body = new TextPart("plain") { Text = body };

        using var output = new MemoryStream();
        message.WriteTo(output);
        return output.ToArray();
    }

    private static SmtpReceiveRequest CreateRequest(byte[] messageData) =>
        new(
            HeloHost: "client.example",
            IsExtendedSmtp: true,
            MailFrom: "sender@example.test",
            Recipients:
            [
                new SmtpResolvedRecipient(
                    "recipient@example.test",
                    "recipient@example.test",
                    LocalAccountId: 0,
                    IsLocal: false)
            ],
            DeclaredSize: null,
            MessageData: messageData,
            ReceivedUtc: DateTimeOffset.UtcNow);

    private sealed class FakeDnsAddressResolver : IDnsAddressResolver
    {
        private readonly Func<string, IReadOnlyList<IPAddress>> _resolve;

        public FakeDnsAddressResolver(Func<string, IReadOnlyList<IPAddress>> resolve)
        {
            _resolve = resolve;
        }

        public List<string> Queries { get; } = [];

        public ValueTask<IReadOnlyList<IPAddress>> ResolveAddressesAsync(
            string hostName,
            CancellationToken cancellationToken)
        {
            Queries.Add(hostName);
            return ValueTask.FromResult(_resolve(hostName));
        }
    }
}
