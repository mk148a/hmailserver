using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SmtpSenderDomainMxCheckerTests
{
    [TestMethod]
    public async Task CheckAsync_RejectsSenderDomainWithoutMxRecords()
    {
        var resolver = new FakeMxResolver(_ => []);
        var checker = new SmtpSenderDomainMxChecker(
            resolver,
            new SmtpSenderDomainMxCheckOptions
            {
                Enabled = true,
                RejectionMessageTemplate = "No MX for {SenderDomain} ({Reason})"
            });

        var result = await checker.CheckAsync(
            CreateRequest("sender@example.test"),
            CancellationToken.None);

        Assert.IsTrue(result.Rejected);
        Assert.AreEqual("example.test", result.SenderDomain);
        Assert.AreEqual("missing-mx", result.FailureReason);
        Assert.AreEqual("554 No MX for example.test (missing-mx)", result.FailureResponse);
        CollectionAssert.AreEqual(new[] { "example.test" }, resolver.Queries.ToArray());
    }

    [TestMethod]
    public async Task CheckAsync_PassesSenderDomainWithMxRecords()
    {
        var resolver = new FakeMxResolver(
            _ => [new DnsMxRecord("mx.example.test", 10, TimeSpan.FromMinutes(5))]);
        var checker = new SmtpSenderDomainMxChecker(
            resolver,
            new SmtpSenderDomainMxCheckOptions { Enabled = true });

        var result = await checker.CheckAsync(
            CreateRequest("sender@example.test"),
            CancellationToken.None);

        Assert.IsFalse(result.Rejected);
        CollectionAssert.AreEqual(new[] { "example.test" }, resolver.Queries.ToArray());
    }

    [TestMethod]
    public async Task CheckAsync_SkipsAuthenticatedClientsByDefault()
    {
        var resolver = new FakeMxResolver(_ => []);
        var checker = new SmtpSenderDomainMxChecker(
            resolver,
            new SmtpSenderDomainMxCheckOptions { Enabled = true });

        var result = await checker.CheckAsync(
            CreateRequest("sender@example.test") with { IsAuthenticated = true },
            CancellationToken.None);

        Assert.IsFalse(result.Rejected);
        Assert.AreEqual(0, resolver.Queries.Count);
    }

    [TestMethod]
    public async Task CheckAsync_SkipsNullReversePathAndDomainLiterals()
    {
        var resolver = new FakeMxResolver(_ => []);
        var checker = new SmtpSenderDomainMxChecker(
            resolver,
            new SmtpSenderDomainMxCheckOptions { Enabled = true });

        var nullReversePath = await checker.CheckAsync(
            CreateRequest(string.Empty),
            CancellationToken.None);
        var domainLiteral = await checker.CheckAsync(
            CreateRequest("sender@[192.0.2.5]"),
            CancellationToken.None);

        Assert.IsFalse(nullReversePath.Rejected);
        Assert.IsFalse(domainLiteral.Rejected);
        Assert.AreEqual(0, resolver.Queries.Count);
    }

    [TestMethod]
    public async Task CheckAsync_FailsOpenWhenResolverThrows()
    {
        var resolver = new FakeMxResolver(_ => throw new IOException("dns failed"));
        var checker = new SmtpSenderDomainMxChecker(
            resolver,
            new SmtpSenderDomainMxCheckOptions { Enabled = true });

        var result = await checker.CheckAsync(
            CreateRequest("sender@example.test"),
            CancellationToken.None);

        Assert.IsFalse(result.Rejected);
        CollectionAssert.AreEqual(new[] { "example.test" }, resolver.Queries.ToArray());
    }

    private static SmtpReceiveRequest CreateRequest(string mailFrom) =>
        new(
            HeloHost: "client.example",
            IsExtendedSmtp: true,
            MailFrom: mailFrom,
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
            ClientIPAddress: "192.0.2.5");

    private sealed class FakeMxResolver : IDnsMxResolver
    {
        private readonly Func<string, IReadOnlyList<DnsMxRecord>> _resolve;

        public FakeMxResolver(Func<string, IReadOnlyList<DnsMxRecord>> resolve)
        {
            _resolve = resolve;
        }

        public List<string> Queries { get; } = [];

        public ValueTask<IReadOnlyList<DnsMxRecord>> ResolveMxAsync(
            string domainName,
            CancellationToken cancellationToken)
        {
            Queries.Add(domainName);
            return ValueTask.FromResult(_resolve(domainName));
        }
    }
}
