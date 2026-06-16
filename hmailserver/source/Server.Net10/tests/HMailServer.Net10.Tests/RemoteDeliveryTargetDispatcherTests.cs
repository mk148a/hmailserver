using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class RemoteDeliveryTargetDispatcherTests
{
    [TestMethod]
    public async Task DispatchAsync_SendsRemoteBatchThroughSmtpClient()
    {
        var message = CreateMessage();
        var batch = new DeliveryTargetBatch(
            new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:example.net", "example.net"),
            message.Recipients);
        var endpoint = new RemoteSmtpEndpoint("mx.example.net", 25, RemoteSmtpConnectionSecurity.None);
        var endpointResolver = new FakeEndpointResolver(endpoint);
        var content = new FakeContentSource("Subject: Test\r\n\r\nHello\r\n"u8.ToArray());
        var smtpClient = new FakeRemoteSmtpClient(RemoteSmtpSendResult.Success());
        var dispatcher = new RemoteDeliveryTargetDispatcher(
            endpointResolver,
            content,
            smtpClient,
            new RemoteDeliveryOptions("mail.local.test", TimeSpan.FromMinutes(2)));

        var result = await dispatcher.DispatchAsync(message, batch, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(smtpClient.LastRequest);
        Assert.AreEqual(endpoint, smtpClient.LastRequest.Endpoint);
        Assert.AreEqual("mail.local.test", smtpClient.LastRequest.HeloHost);
        CollectionAssert.AreEqual(
            new[] { "user1@example.net", "user2@example.net" },
            smtpClient.LastRequest.RecipientAddresses.ToArray());
    }

    [TestMethod]
    public async Task DispatchAsync_PreservesPermanentFailureKindFromSmtpClient()
    {
        var message = CreateMessage();
        var batch = new DeliveryTargetBatch(
            new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:example.net", "example.net"),
            message.Recipients);
        var dispatcher = new RemoteDeliveryTargetDispatcher(
            new FakeEndpointResolver(new RemoteSmtpEndpoint("mx.example.net", 25, RemoteSmtpConnectionSecurity.None)),
            new FakeContentSource("Subject: Test\r\n\r\nHello\r\n"u8.ToArray()),
            new FakeRemoteSmtpClient(RemoteSmtpSendResult.Failure("550 No such user.", failureKind: DeliveryFailureKind.Permanent)),
            new RemoteDeliveryOptions("mail.local.test", TimeSpan.FromMinutes(2)));

        var result = await dispatcher.DispatchAsync(message, batch, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(DeliveryFailureKind.Permanent, result.FailureKind);
        StringAssert.Contains(result.Error, "550");
    }

    [TestMethod]
    public async Task DispatchAsync_DefersWhenContentCannotBeLoaded()
    {
        var message = CreateMessage();
        var batch = new DeliveryTargetBatch(
            new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:example.net", "example.net"),
            message.Recipients);
        var dispatcher = new RemoteDeliveryTargetDispatcher(
            new FakeEndpointResolver(new RemoteSmtpEndpoint("mx.example.net", 25, RemoteSmtpConnectionSecurity.None)),
            new FakeContentSource(null),
            new FakeRemoteSmtpClient(RemoteSmtpSendResult.Success()),
            new RemoteDeliveryOptions("mail.local.test", TimeSpan.FromMinutes(2)));

        var result = await dispatcher.DispatchAsync(message, batch, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(DeliveryFailureKind.Transient, result.FailureKind);
        Assert.AreEqual(TimeSpan.FromMinutes(2), result.RetryDelay);
        StringAssert.Contains(result.Error, "content");
    }

    private static DeliveryQueuedMessage CreateMessage() =>
        new(
            new MessageIdentity(100, 0, 0, 0),
            "queue.eml",
            "sender@example.test",
            Size: 1234,
            CreatedUtc: DateTimeOffset.Parse("2026-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture),
            Flags: ImapMessageFlags.Recent,
            CurrentRetryCount: 0,
            Recipients:
            [
                new DeliveryQueueRecipient(1, "user1@example.net", "user1@example.net", LocalAccountId: 0),
                new DeliveryQueueRecipient(2, "user2@example.net", "user2@example.net", LocalAccountId: 0)
            ]);

    private sealed class FakeEndpointResolver : IRemoteSmtpEndpointResolver
    {
        private readonly RemoteSmtpEndpoint _endpoint;

        public FakeEndpointResolver(RemoteSmtpEndpoint endpoint)
        {
            _endpoint = endpoint;
        }

        public ValueTask<RemoteSmtpEndpoint> ResolveAsync(
            DeliveryTarget target,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_endpoint);
    }

    private sealed class FakeContentSource : IDeliveryMessageContentSource
    {
        private readonly byte[]? _content;

        public FakeContentSource(byte[]? content)
        {
            _content = content;
        }

        public ValueTask<byte[]?> TryLoadAsync(
            DeliveryQueuedMessage message,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_content);
    }

    private sealed class FakeRemoteSmtpClient : IRemoteSmtpClient
    {
        private readonly RemoteSmtpSendResult _result;

        public FakeRemoteSmtpClient(RemoteSmtpSendResult result)
        {
            _result = result;
        }

        public RemoteSmtpSendRequest? LastRequest { get; private set; }

        public ValueTask<RemoteSmtpSendResult> SendAsync(
            RemoteSmtpSendRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return ValueTask.FromResult(_result);
        }
    }
}
