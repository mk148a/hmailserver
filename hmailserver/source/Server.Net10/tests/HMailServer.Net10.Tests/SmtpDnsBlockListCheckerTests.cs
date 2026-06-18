using System.Net;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SmtpDnsBlockListCheckerTests
{
    [TestMethod]
    public async Task CheckAsync_BuildsReversedIpv4QueryAndBlocksOnDnsHit()
    {
        var resolver = new FakeDnsAddressResolver(hostName =>
            hostName == "5.2.0.192.zen.example.test"
                ? [IPAddress.Parse("127.0.0.2")]
                : []);
        var checker = new SmtpDnsBlockListChecker(
            resolver,
            new SmtpDnsBlockListOptions
            {
                Enabled = true,
                Zones = ["zen.example.test."],
                RejectionMessageTemplate = "Blocked by {ListHost} ({ResponseAddress})"
            });

        var result = await checker.CheckAsync(
            CreateRequest("192.0.2.5"),
            CancellationToken.None);

        Assert.IsTrue(result.Listed);
        Assert.AreEqual("zen.example.test", result.ListHost);
        Assert.AreEqual("5.2.0.192.zen.example.test", result.QueryHost);
        Assert.AreEqual("127.0.0.2", result.ResponseAddress);
        Assert.AreEqual("554 Blocked by zen.example.test (127.0.0.2)", result.FailureResponse);
        CollectionAssert.AreEqual(
            new[] { "5.2.0.192.zen.example.test" },
            resolver.Queries.ToArray());
    }

    [TestMethod]
    public async Task CheckAsync_SkipsAuthenticatedClientsByDefault()
    {
        var resolver = new FakeDnsAddressResolver(_ => [IPAddress.Parse("127.0.0.2")]);
        var checker = new SmtpDnsBlockListChecker(
            resolver,
            new SmtpDnsBlockListOptions
            {
                Enabled = true,
                Zones = ["zen.example.test"]
            });

        var result = await checker.CheckAsync(
            CreateRequest("192.0.2.5") with { IsAuthenticated = true },
            CancellationToken.None);

        Assert.IsFalse(result.Listed);
        Assert.AreEqual(0, resolver.Queries.Count);
    }

    [TestMethod]
    public async Task CheckAsync_FailsOpenWhenResolverThrows()
    {
        var resolver = new FakeDnsAddressResolver(_ => throw new IOException("dns failed"));
        var checker = new SmtpDnsBlockListChecker(
            resolver,
            new SmtpDnsBlockListOptions
            {
                Enabled = true,
                Zones = ["zen.example.test"]
            });

        var result = await checker.CheckAsync(
            CreateRequest("192.0.2.5"),
            CancellationToken.None);

        Assert.IsFalse(result.Listed);
        CollectionAssert.AreEqual(
            new[] { "5.2.0.192.zen.example.test" },
            resolver.Queries.ToArray());
    }

    private static SmtpReceiveRequest CreateRequest(string clientAddress) =>
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
            MessageData: "Subject: Test\r\n\r\nBody\r\n"u8.ToArray(),
            ReceivedUtc: DateTimeOffset.UtcNow,
            ClientIPAddress: clientAddress);

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
