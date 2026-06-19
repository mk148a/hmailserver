using System.Net;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SmtpReverseDnsCheckerTests
{
    [TestMethod]
    public async Task CheckAsync_RejectsMissingPtr()
    {
        var checker = new SmtpReverseDnsChecker(
            new FakeReverseResolver([]),
            new FakeAddressResolver(),
            new SmtpReverseDnsCheckOptions
            {
                Enabled = true,
                RejectionMessageTemplate = "Rejected {ClientIP} {Reason}"
            });

        var result = await checker.CheckAsync(CreateRequest("192.0.2.5"), CancellationToken.None);

        Assert.IsTrue(result.Rejected);
        Assert.AreEqual("192.0.2.5", result.ClientIPAddress);
        Assert.AreEqual("missing-ptr", result.FailureReason);
        Assert.AreEqual("554 Rejected 192.0.2.5 missing-ptr", result.FailureResponse);
    }

    [TestMethod]
    public async Task CheckAsync_PassesForwardConfirmedPtr()
    {
        var checker = new SmtpReverseDnsChecker(
            new FakeReverseResolver(["mail.example.test"]),
            new FakeAddressResolver
            {
                ["mail.example.test"] = [IPAddress.Parse("192.0.2.5")]
            },
            new SmtpReverseDnsCheckOptions
            {
                Enabled = true,
                RequireForwardConfirmed = true
            });

        var result = await checker.CheckAsync(CreateRequest("192.0.2.5"), CancellationToken.None);

        Assert.IsFalse(result.Rejected);
    }

    [TestMethod]
    public async Task CheckAsync_RejectsForwardMismatch()
    {
        var checker = new SmtpReverseDnsChecker(
            new FakeReverseResolver(["mail.example.test"]),
            new FakeAddressResolver
            {
                ["mail.example.test"] = [IPAddress.Parse("198.51.100.25")]
            },
            new SmtpReverseDnsCheckOptions
            {
                Enabled = true,
                RequireForwardConfirmed = true
            });

        var result = await checker.CheckAsync(CreateRequest("192.0.2.5"), CancellationToken.None);

        Assert.IsTrue(result.Rejected);
        Assert.AreEqual("forward-confirmation-failed", result.FailureReason);
        CollectionAssert.AreEqual(new[] { "mail.example.test" }, result.HostNames.ToArray());
    }

    [TestMethod]
    public async Task CheckAsync_SkipsAuthenticatedByDefault()
    {
        var checker = new SmtpReverseDnsChecker(
            new FakeReverseResolver([]),
            new FakeAddressResolver(),
            new SmtpReverseDnsCheckOptions { Enabled = true });

        var result = await checker.CheckAsync(
            CreateRequest("192.0.2.5") with { IsAuthenticated = true },
            CancellationToken.None);

        Assert.IsFalse(result.Rejected);
    }

    private static SmtpReceiveRequest CreateRequest(string clientIp) =>
        new(
            HeloHost: "client.example.test",
            IsExtendedSmtp: true,
            MailFrom: "sender@example.test",
            Recipients: [new SmtpResolvedRecipient("user@example.test", "user@example.test", 10, IsLocal: true)],
            DeclaredSize: null,
            MessageData: "Subject: Test\r\n\r\nBody\r\n"u8.ToArray(),
            ReceivedUtc: DateTimeOffset.UtcNow,
            ClientIPAddress: clientIp);

    private sealed class FakeReverseResolver : IDnsReverseResolver
    {
        private readonly IReadOnlyList<string> _hostNames;

        public FakeReverseResolver(IReadOnlyList<string> hostNames)
        {
            _hostNames = hostNames;
        }

        public ValueTask<IReadOnlyList<string>> ResolveHostNamesAsync(
            IPAddress address,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_hostNames);
    }

    private sealed class FakeAddressResolver : Dictionary<string, IReadOnlyList<IPAddress>>, IDnsAddressResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAddressesAsync(
            string hostName,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(
                TryGetValue(hostName, out var addresses)
                    ? addresses
                    : Array.Empty<IPAddress>());
    }
}
