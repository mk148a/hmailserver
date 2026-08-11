using System.Net;
using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Smtp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SmtpSessionTests
{
    [TestMethod]
    public async Task RunAsync_HandlesEhloNoopRsetAndQuit()
    {
        await using var stream = new DuplexMemoryStream("EHLO client.example\r\nNOOP\r\nRSET\r\nQUIT\r\n");
        var session = new SmtpSession(new SmtpSessionOptions { ServerName = "mx.example.test" });

        await session.RunAsync(stream, CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "220 hMailServer .NET 10 ESMTP ready\r\n");
        StringAssert.Contains(output, "250-mx.example.test\r\n250-SIZE 20971520\r\n250 HELP\r\n");
        StringAssert.Contains(output, "250 OK\r\n250 OK\r\n");
        StringAssert.Contains(output, "221 mx.example.test closing connection\r\n");
    }

    [TestMethod]
    public async Task RunAsync_UsesLegacySettingsBackedGreetingFormatting(
        )
    {
        var cases = new Dictionary<string, string>
        {
            [string.Empty] = "220 mx.example.test ESMTP\r\n",
            ["custom welcome"] = "220 custom welcome ESMTP\r\n",
            ["custom welcome ESMTP"] = "220 custom welcome ESMTP\r\n"
        };

        foreach (var (welcomeSmtp, expectedGreeting) in cases)
        {
            await using var stream = new DuplexMemoryStream("QUIT\r\n");
            var session = new SmtpSession(new SmtpSessionOptions
            {
                ServerName = "mx.example.test",
                GreetingProvider = () => welcomeSmtp
            });

            await session.RunAsync(stream, CancellationToken.None);

            StringAssert.StartsWith(stream.GetOutputText(), expectedGreeting);
        }
    }

    [TestMethod]
    public async Task RunAsync_HandlesHelo()
    {
        await using var stream = new DuplexMemoryStream("HELO client.example\r\nQUIT\r\n");
        var session = new SmtpSession(new SmtpSessionOptions { ServerName = "mx.example.test" });

        await session.RunAsync(stream, CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "250 mx.example.test\r\n");
    }

    [TestMethod]
    public async Task RunAsync_RunsOnHeloEventBeforeEhloResponse()
    {
        await using var stream = new DuplexMemoryStream("EHLO bad.example\r\nNOOP\r\nQUIT\r\n");
        SmtpEventScriptExecutionRequest? capturedRequest = null;
        var session = new SmtpSession(
            new SmtpSessionOptions { ServerName = "mx.example.test" },
            eventScriptExecutor: new FakeEventScriptExecutor(
                request =>
                {
                    capturedRequest = request;
                    return SmtpRuleScriptExecutionResult.Failure("554 bad helo");
                }));

        await session.RunAsync(
            stream,
            startTlsStreamProvider: null,
            connectionContext: new SmtpSessionConnectionContext(
                ClientIPAddress: "203.0.113.10",
                ClientPort: 2525,
                SessionId: 42),
            cancellationToken: CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "554 bad helo\r\n");
        Assert.IsFalse(output.Contains("250-mx.example.test\r\n", StringComparison.Ordinal));
        StringAssert.Contains(output, "250 OK\r\n");
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual("OnHELO", capturedRequest.EventName);
        Assert.AreEqual(SmtpEventScriptArgumentShape.ClientOnly, capturedRequest.ArgumentShape);
        Assert.AreEqual("bad.example", capturedRequest.Client.HeloHost);
        Assert.AreEqual("203.0.113.10", capturedRequest.Client.IPAddress);
        Assert.AreEqual(2525, capturedRequest.Client.Port);
        Assert.AreEqual(42, capturedRequest.Client.SessionId);
    }

    [TestMethod]
    public async Task RunAsync_RejectsEhloWithoutHostName()
    {
        await using var stream = new DuplexMemoryStream("EHLO\r\nQUIT\r\n");
        var session = new SmtpSession();

        await session.RunAsync(stream, CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "501 Syntax: EHLO hostname\r\n");
    }

    [TestMethod]
    public async Task RunAsync_StagesMailRcptAndDataThroughReceiver()
    {
        await using var stream = new DuplexMemoryStream(
            "EHLO client.example\r\nMAIL FROM:<sender@example.test> SIZE=18\r\nRCPT TO:<recipient@example.test>\r\nDATA\r\nSubject: Test\r\n\r\n..Body\r\n.\r\nQUIT\r\n");
        var receiver = new FakeMessageReceiver();
        var session = new SmtpSession(
            new SmtpSessionOptions { ServerName = "mx.example.test" },
            receiver);

        await session.RunAsync(stream, CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "250 OK\r\n250 OK\r\n");
        StringAssert.Contains(output, "354 Start mail input; end with <CRLF>.<CRLF>\r\n");
        StringAssert.Contains(output, "250 Queued\r\n");
        Assert.IsNotNull(receiver.LastRequest);
        Assert.AreEqual("client.example", receiver.LastRequest.HeloHost);
        Assert.IsTrue(receiver.LastRequest.IsExtendedSmtp);
        Assert.AreEqual("sender@example.test", receiver.LastRequest.MailFrom);
        CollectionAssert.AreEqual(new[] { "recipient@example.test" }, receiver.LastRequest.Recipients.Select(static recipient => recipient.Address).ToArray());
        Assert.AreEqual(18L, receiver.LastRequest.DeclaredSize);
        Assert.AreEqual(string.Empty, receiver.LastRequest.ClientIPAddress);
        Assert.AreEqual(0, receiver.LastRequest.ClientPort);
        Assert.IsTrue(receiver.LastRequest.SessionId > 0);
        Assert.AreEqual("Subject: Test\r\n\r\n.Body\r\n", Encoding.Latin1.GetString(receiver.LastRequest.MessageData));
    }

    [TestMethod]
    public async Task RunAsync_PassesConnectionContextThroughReceiver()
    {
        await using var stream = new DuplexMemoryStream(
            "EHLO client.example\r\nMAIL FROM:<sender@example.test>\r\nRCPT TO:<recipient@example.test>\r\nDATA\r\nSubject: Test\r\n\r\nBody\r\n.\r\nQUIT\r\n");
        var receiver = new FakeMessageReceiver();
        var session = new SmtpSession(
            new SmtpSessionOptions { ServerName = "mx.example.test" },
            receiver);

        await session.RunAsync(
            stream,
            startTlsStreamProvider: null,
            connectionContext: new SmtpSessionConnectionContext(
                ClientIPAddress: "203.0.113.10",
                ClientPort: 2525,
                SessionId: 99),
            cancellationToken: CancellationToken.None);

        Assert.IsNotNull(receiver.LastRequest);
        Assert.AreEqual("203.0.113.10", receiver.LastRequest.ClientIPAddress);
        Assert.AreEqual(2525, receiver.LastRequest.ClientPort);
        Assert.AreEqual(99, receiver.LastRequest.SessionId);
    }

    [TestMethod]
    public async Task RunAsync_PassesOnSmtpDataMutatedMessageToReceiver()
    {
        await using var stream = new DuplexMemoryStream(
            "EHLO client.example\r\nMAIL FROM:<sender@example.test>\r\nRCPT TO:<recipient@example.test>\r\nDATA\r\nSubject: Original\r\n\r\nBody\r\n.\r\nQUIT\r\n");
        var receiver = new FakeMessageReceiver();
        var mutatedMessage = Encoding.Latin1.GetBytes("Subject: Mutated\r\nX-SMTPData: yes\r\n\r\nBody\r\n");
        SmtpEventScriptExecutionRequest? capturedRequest = null;
        var session = new SmtpSession(
            new SmtpSessionOptions { ServerName = "mx.example.test" },
            receiver,
            eventScriptExecutor: new FakeEventScriptExecutor(
                request =>
                {
                    if (request.EventName != "OnSMTPData")
                    {
                        return SmtpRuleScriptExecutionResult.Continue(request.MessageData);
                    }

                    capturedRequest = request;
                    return SmtpRuleScriptExecutionResult.Continue(mutatedMessage);
                }));

        await session.RunAsync(stream, CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "250 Queued\r\n");
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(SmtpEventScriptArgumentShape.ClientAndMessage, capturedRequest.ArgumentShape);
        Assert.IsNotNull(receiver.LastRequest);
        StringAssert.Contains(
            Encoding.Latin1.GetString(receiver.LastRequest.MessageData),
            "X-SMTPData: yes\r\n");
    }

    [TestMethod]
    public async Task RunAsync_ReturnsFailureBeforeReceiverWhenOnSmtpDataRejects()
    {
        await using var stream = new DuplexMemoryStream(
            "EHLO client.example\r\nMAIL FROM:<sender@example.test>\r\nRCPT TO:<recipient@example.test>\r\nDATA\r\nSubject: Block\r\n\r\nBody\r\n.\r\nQUIT\r\n");
        var receiver = new FakeMessageReceiver();
        var session = new SmtpSession(
            new SmtpSessionOptions { ServerName = "mx.example.test" },
            receiver,
            eventScriptExecutor: new FakeEventScriptExecutor(
                request => request.EventName == "OnSMTPData"
                    ? SmtpRuleScriptExecutionResult.Failure("554 data blocked")
                    : SmtpRuleScriptExecutionResult.Continue(request.MessageData)));

        await session.RunAsync(stream, CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "554 data blocked\r\n");
        Assert.IsFalse(output.Contains("250 Queued\r\n", StringComparison.Ordinal));
        Assert.IsNull(receiver.LastRequest);
    }

    [TestMethod]
    public async Task RunAsync_RejectsRecipientWhenValidatorRejects()
    {
        await using var stream = new DuplexMemoryStream(
            "EHLO client.example\r\nMAIL FROM:<sender@example.test>\r\nRCPT TO:<missing@example.test>\r\nQUIT\r\n");
        var receiver = new FakeMessageReceiver();
        var validator = new FakeRecipientValidator(SmtpRecipientValidationResult.Reject("550 Unknown user"));
        SmtpEventScriptExecutionRequest? capturedRequest = null;
        var session = new SmtpSession(
            new SmtpSessionOptions(),
            receiver,
            validator,
            eventScriptExecutor: new FakeEventScriptExecutor(
                request =>
                {
                    if (request.EventName == "OnRecipientUnknown")
                    {
                        capturedRequest = request;
                    }

                    return SmtpRuleScriptExecutionResult.Continue(request.MessageData);
                }));

        await session.RunAsync(stream, CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "550 Unknown user\r\n");
        Assert.IsNull(receiver.LastRequest);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(SmtpEventScriptArgumentShape.ClientAndMessage, capturedRequest.ArgumentShape);
        Assert.AreEqual("client.example", capturedRequest.Client.HeloHost);
    }

    [TestMethod]
    public async Task RunAsync_StoresResolvedRecipientFromValidator()
    {
        await using var stream = new DuplexMemoryStream(
            "EHLO client.example\r\nMAIL FROM:<sender@example.test>\r\nRCPT TO:<user+tag@example.test>\r\nDATA\r\nSubject: Test\r\n.\r\nQUIT\r\n");
        var receiver = new FakeMessageReceiver();
        var validator = new FakeRecipientValidator(
            SmtpRecipientValidationResult.Accept(
                new SmtpResolvedRecipient(
                    "user@example.test",
                    "user+tag@example.test",
                    LocalAccountId: 42,
                    IsLocal: true)));
        var session = new SmtpSession(
            new SmtpSessionOptions(),
            receiver,
            validator);

        await session.RunAsync(stream, CancellationToken.None);

        Assert.IsNotNull(receiver.LastRequest);
        var recipient = receiver.LastRequest.Recipients.Single();
        Assert.AreEqual("user@example.test", recipient.Address);
        Assert.AreEqual("user+tag@example.test", recipient.OriginalAddress);
        Assert.AreEqual(42, recipient.LocalAccountId);
        Assert.IsTrue(recipient.IsLocal);
    }

    [TestMethod]
    public async Task RunAsync_StoresRouteResolvedRecipientFromValidator()
    {
        await using var stream = new DuplexMemoryStream(
            "EHLO client.example\r\nMAIL FROM:<sender@example.test>\r\nRCPT TO:<user@route.example>\r\nDATA\r\nSubject: Test\r\n.\r\nQUIT\r\n");
        var receiver = new FakeMessageReceiver();
        var validator = new FakeRecipientValidator(
            SmtpRecipientValidationResult.Accept(
                new SmtpResolvedRecipient(
                    "user@route.example",
                    "user@route.example",
                    LocalAccountId: 0,
                    IsLocal: true,
                    Route: new SmtpRouteResolution(
                        RouteId: 9,
                        DomainName: "*.route.example",
                        TargetHost: "relay.route.example",
                        TargetPort: 2525,
                        ConnectionSecurity: 1,
                        TreatRecipientAsLocal: true))));
        var session = new SmtpSession(
            new SmtpSessionOptions(),
            receiver,
            validator);

        await session.RunAsync(stream, CancellationToken.None);

        Assert.IsNotNull(receiver.LastRequest);
        var recipient = receiver.LastRequest.Recipients.Single();
        Assert.IsTrue(recipient.IsRouteRecipient);
        Assert.IsTrue(recipient.RouteTreatsRecipientAsLocal);
        Assert.AreEqual(9, recipient.Route?.RouteId);
        Assert.AreEqual("relay.route.example", recipient.Route?.TargetHost);
    }

    [TestMethod]
    public async Task RunAsync_RejectsMailWhenDeclaredSizeExceedsLimit()
    {
        await using var stream = new DuplexMemoryStream("EHLO client.example\r\nMAIL FROM:<sender@example.test> SIZE=11\r\nQUIT\r\n");
        var receiver = new FakeMessageReceiver();
        var session = new SmtpSession(
            new SmtpSessionOptions { MaxMessageBytes = 10 },
            receiver);

        await session.RunAsync(stream, CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "552 Message size exceeds fixed maximum message size\r\n");
        Assert.IsNull(receiver.LastRequest);
    }

    [TestMethod]
    public async Task RunAsync_RejectsDataWhenActualSizeExceedsLimitAndDrainsMessage()
    {
        await using var stream = new DuplexMemoryStream(
            "EHLO client.example\r\nMAIL FROM:<sender@example.test>\r\nRCPT TO:<recipient@example.test>\r\nDATA\r\n123456\r\n.\r\nNOOP\r\nQUIT\r\n");
        var receiver = new FakeMessageReceiver();
        var session = new SmtpSession(
            new SmtpSessionOptions { MaxMessageBytes = 5 },
            receiver);

        await session.RunAsync(stream, CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "354 Start mail input; end with <CRLF>.<CRLF>\r\n");
        StringAssert.Contains(output, "552 Message size exceeds fixed maximum message size\r\n250 OK\r\n");
        Assert.IsNull(receiver.LastRequest);
    }

    [TestMethod]
    public async Task RunAsync_RejectsRcptAndDataOutOfSequence()
    {
        await using var stream = new DuplexMemoryStream(
            "EHLO client.example\r\nRCPT TO:<recipient@example.test>\r\nDATA\r\nQUIT\r\n");
        var session = new SmtpSession();

        await session.RunAsync(stream, CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "503 Need MAIL command\r\n");
        StringAssert.Contains(output, "503 Need MAIL command\r\n");
    }

    [TestMethod]
    public async Task RunAsync_FiresOnTooManyInvalidCommandsAndDisconnects()
    {
        await using var stream = new DuplexMemoryStream("EHLO client.example\r\nBAD\r\nNOPE\r\nNOOP\r\n");
        SmtpEventScriptExecutionRequest? capturedRequest = null;
        var session = new SmtpSession(
            new SmtpSessionOptions
            {
                ServerName = "mx.example.test",
                DisconnectInvalidClients = true,
                MaximumIncorrectCommands = 1
            },
            eventScriptExecutor: new FakeEventScriptExecutor(
                request =>
                {
                    if (request.EventName == "OnTooManyInvalidCommands")
                    {
                        capturedRequest = request;
                    }

                    return SmtpRuleScriptExecutionResult.Continue(request.MessageData);
                }));

        await session.RunAsync(stream, CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "502 Command not implemented\r\n");
        StringAssert.Contains(output, "Too many invalid commands. Bye!\r\n");
        Assert.IsFalse(output.Contains("250 OK\r\n", StringComparison.Ordinal));
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(SmtpEventScriptArgumentShape.ClientAndMessage, capturedRequest.ArgumentShape);
        Assert.AreEqual("client.example", capturedRequest.Client.HeloHost);
    }

    [TestMethod]
    public async Task RunAsync_ReturnsTemporaryFailureWhenReceiverIsNotConfigured()
    {
        await using var stream = new DuplexMemoryStream(
            "EHLO client.example\r\nMAIL FROM:<sender@example.test>\r\nRCPT TO:<recipient@example.test>\r\nDATA\r\nSubject: Test\r\n.\r\nQUIT\r\n");
        var session = new SmtpSession();

        await session.RunAsync(stream, CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "451 Requested action aborted: local error in processing\r\n");
    }

    [TestMethod]
    public async Task RunAsync_AuthPlainEnablesAuthenticatedRelay()
    {
        var authToken = EncodeAuthPlain("user@example.test", "secret");
        await using var stream = new DuplexMemoryStream(
            $"EHLO client.example\r\nAUTH PLAIN {authToken}\r\nMAIL FROM:<user@example.test>\r\nRCPT TO:<remote@example.net>\r\nDATA\r\nSubject: Test\r\n.\r\nQUIT\r\n");
        var receiver = new FakeMessageReceiver();
        var validator = new AuthenticatedRelayValidator();
        var session = new SmtpSession(
            new SmtpSessionOptions(),
            receiver,
            validator,
            new FakeAccountAuthenticator());

        await session.RunAsync(stream, CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "250-AUTH PLAIN LOGIN\r\n");
        StringAssert.Contains(output, "235 Authentication successful\r\n");
        StringAssert.Contains(output, "250 Queued\r\n");
        Assert.IsTrue(validator.LastRequest?.SenderAuthenticated);
        Assert.IsNotNull(receiver.LastRequest);
        Assert.AreEqual("remote@example.net", receiver.LastRequest.Recipients.Single().Address);
    }

    [TestMethod]
    public async Task RunAsync_UsesInjectedBoundaryWithSmtpCallerAndRemoteAddress()
    {
        var authToken = EncodeAuthPlain("user@example.test", "secret");
        await using var stream = new DuplexMemoryStream(
            $"EHLO client.example\r\nAUTH PLAIN {authToken}\r\nQUIT\r\n");
        var boundary = new CapturingClientAwareAuthenticationService(
            ImapAuthenticationResult.Success(new ImapAuthenticatedAccount(77, "user@example.test")));
        var session = new SmtpSession(
            new SmtpSessionOptions(),
            accountAuthenticator: new FakeAccountAuthenticator(),
            clientAwareAuthenticationService: boundary);

        await session.RunAsync(
            stream,
            startTlsStreamProvider: null,
            connectionContext: new SmtpSessionConnectionContext(
                ClientIPAddress: "203.0.113.33",
                ClientPort: 2525,
                SessionId: 48),
            cancellationToken: CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "235 Authentication successful\r\n");
        Assert.IsNotNull(boundary.LastRequest);
        Assert.AreEqual("user@example.test", boundary.LastRequest.Username);
        Assert.AreEqual("secret", boundary.LastRequest.Password);
        Assert.AreEqual(IPAddress.Parse("203.0.113.33"), boundary.LastRequest.ClientAddress);
        Assert.AreEqual(ClientAuthenticationCaller.Smtp, boundary.LastRequest.Caller);
    }

    [TestMethod]
    public async Task RunAsync_UsesInjectedFailureAndDisconnectsForSmtp()
    {
        var authToken = EncodeAuthPlain("user@example.test", "wrong");
        await using var stream = new DuplexMemoryStream(
            $"EHLO client.example\r\nAUTH PLAIN {authToken}\r\nNOOP\r\n");
        var boundary = new CapturingClientAwareAuthenticationService(
            ImapAuthenticationResult.Failure("Injected authentication failure."),
            disconnect: true);
        var session = new SmtpSession(
            new SmtpSessionOptions(),
            accountAuthenticator: new FakeAccountAuthenticator(),
            clientAwareAuthenticationService: boundary);

        await session.RunAsync(
            stream,
            startTlsStreamProvider: null,
            connectionContext: new SmtpSessionConnectionContext(
                ClientIPAddress: "203.0.113.36",
                ClientPort: 2525,
                SessionId: 50),
            cancellationToken: CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "535 Authentication failed\r\n");
        Assert.IsFalse(output.Contains("235 Authentication successful\r\n", StringComparison.Ordinal));
        Assert.IsFalse(output.Contains("250 OK\r\n", StringComparison.Ordinal));
        Assert.IsNotNull(boundary.LastRequest);
        Assert.AreEqual(ClientAuthenticationCaller.Smtp, boundary.LastRequest.Caller);
    }

    [TestMethod]
    public async Task RunAsync_RunsOnClientLogonAfterSuccessfulAuth()
    {
        var authToken = EncodeAuthPlain("user@example.test", "secret");
        await using var stream = new DuplexMemoryStream(
            $"EHLO client.example\r\nAUTH PLAIN {authToken}\r\nQUIT\r\n");
        SmtpEventScriptExecutionRequest? capturedRequest = null;
        var session = new SmtpSession(
            new SmtpSessionOptions(),
            messageReceiver: null,
            recipientValidator: null,
            accountAuthenticator: new FakeAccountAuthenticator(),
            eventScriptExecutor: new FakeEventScriptExecutor(
                request =>
                {
                    if (request.EventName == "OnClientLogon")
                    {
                        capturedRequest = request;
                    }

                    return SmtpRuleScriptExecutionResult.Continue(request.MessageData);
                }));

        await session.RunAsync(
            stream,
            startTlsStreamProvider: null,
            connectionContext: new SmtpSessionConnectionContext(
                ClientIPAddress: "203.0.113.11",
                ClientPort: 2525,
                SessionId: 77),
            cancellationToken: CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "235 Authentication successful\r\n");
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual("OnClientLogon", capturedRequest.EventName);
        Assert.AreEqual(SmtpEventScriptArgumentShape.ClientOnly, capturedRequest.ArgumentShape);
        Assert.AreEqual("user@example.test", capturedRequest.Client.Username);
        Assert.IsTrue(capturedRequest.Client.IsAuthenticated);
        Assert.AreEqual("client.example", capturedRequest.Client.HeloHost);
        Assert.AreEqual("203.0.113.11", capturedRequest.Client.IPAddress);
        Assert.AreEqual(77, capturedRequest.Client.SessionId);
    }

    [TestMethod]
    public async Task RunAsync_AdvertisesStartTlsWhenProviderSupportsUpgrade()
    {
        await using var stream = new DuplexMemoryStream("EHLO client.example\r\nQUIT\r\n");
        var startTlsProvider = new FakeStartTlsProvider(stream.CreateContinuation("QUIT\r\n"));
        var session = new SmtpSession(new SmtpSessionOptions { ServerName = "mx.example.test" });

        await session.RunAsync(stream, startTlsProvider, CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "250-STARTTLS\r\n");
        Assert.AreEqual(0, startTlsProvider.UpgradeCount);
    }

    [TestMethod]
    public async Task RunAsync_StartTlsUpgradesStreamAndResetsHeloState()
    {
        await using var stream = new DuplexMemoryStream("EHLO client.example\r\nSTARTTLS\r\n");
        var startTlsProvider = new FakeStartTlsProvider(
            stream.CreateContinuation("MAIL FROM:<sender@example.test>\r\nEHLO secure.example\r\nQUIT\r\n"));
        var session = new SmtpSession(new SmtpSessionOptions { ServerName = "mx.example.test" });

        await session.RunAsync(stream, startTlsProvider, CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "250-STARTTLS\r\n");
        StringAssert.Contains(output, "220 Ready to start TLS\r\n");
        StringAssert.Contains(output, "503 Send HELO/EHLO first\r\n");
        StringAssert.Contains(output, "250-mx.example.test\r\n250-SIZE 20971520\r\n250 HELP\r\n");
        Assert.AreEqual(1, startTlsProvider.UpgradeCount);
    }

    [TestMethod]
    public async Task RunAsync_RejectsStartTlsWhenUnavailable()
    {
        await using var stream = new DuplexMemoryStream("STARTTLS\r\nQUIT\r\n");
        var session = new SmtpSession(new SmtpSessionOptions { ServerName = "mx.example.test" });

        await session.RunAsync(stream, CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "454 TLS not available\r\n");
    }

    [TestMethod]
    public async Task RunAsync_RequiresStartTlsBeforeAuthenticationWhenConfigured()
    {
        var authToken = EncodeAuthPlain("user@example.test", "secret");
        await using var stream = new DuplexMemoryStream(
            $"EHLO client.example\r\nAUTH PLAIN {authToken}\r\nQUIT\r\n");
        var session = new SmtpSession(
            new SmtpSessionOptions { RequireTlsForAuthentication = true },
            messageReceiver: null,
            recipientValidator: null,
            accountAuthenticator: new FakeAccountAuthenticator());

        await session.RunAsync(stream, CancellationToken.None);

        var output = stream.GetOutputText();
        Assert.IsFalse(output.Contains("250-AUTH PLAIN LOGIN\r\n", StringComparison.Ordinal));
        StringAssert.Contains(output, "530 Must issue STARTTLS first\r\n");
    }

    [TestMethod]
    public async Task RunAsync_AllowsAuthenticationAfterStartTlsWhenTlsIsRequired()
    {
        var authToken = EncodeAuthPlain("user@example.test", "secret");
        await using var stream = new DuplexMemoryStream("EHLO client.example\r\nSTARTTLS\r\n");
        var startTlsProvider = new FakeStartTlsProvider(
            stream.CreateContinuation($"EHLO client.example\r\nAUTH PLAIN {authToken}\r\nQUIT\r\n"));
        var session = new SmtpSession(
            new SmtpSessionOptions { RequireTlsForAuthentication = true },
            messageReceiver: null,
            recipientValidator: null,
            accountAuthenticator: new FakeAccountAuthenticator());

        await session.RunAsync(stream, startTlsProvider, CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "250-STARTTLS\r\n");
        StringAssert.Contains(output, "250-AUTH PLAIN LOGIN\r\n");
        StringAssert.Contains(output, "235 Authentication successful\r\n");
        Assert.AreEqual(1, startTlsProvider.UpgradeCount);
    }

    [TestMethod]
    public async Task RunAsync_AuthLoginAuthenticatesWithChallengeFlow()
    {
        await using var stream = new DuplexMemoryStream(
            $"EHLO client.example\r\nAUTH LOGIN\r\n{EncodeAuthToken("user@example.test")}\r\n{EncodeAuthToken("secret")}\r\nQUIT\r\n");
        var session = new SmtpSession(
            new SmtpSessionOptions(),
            messageReceiver: null,
            recipientValidator: null,
            accountAuthenticator: new FakeAccountAuthenticator());

        await session.RunAsync(stream, CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "334 VXNlcm5hbWU6\r\n");
        StringAssert.Contains(output, "334 UGFzc3dvcmQ6\r\n");
        StringAssert.Contains(output, "235 Authentication successful\r\n");
    }

    [TestMethod]
    public async Task RunAsync_AuthPlainRejectsInvalidCredentials()
    {
        var authToken = EncodeAuthPlain("user@example.test", "wrong");
        await using var stream = new DuplexMemoryStream(
            $"EHLO client.example\r\nAUTH PLAIN {authToken}\r\nQUIT\r\n");
        SmtpEventScriptExecutionRequest? capturedRequest = null;
        var session = new SmtpSession(
            new SmtpSessionOptions(),
            messageReceiver: null,
            recipientValidator: null,
            accountAuthenticator: new FakeAccountAuthenticator(),
            eventScriptExecutor: new FakeEventScriptExecutor(
                request =>
                {
                    if (request.EventName == "OnClientLogon")
                    {
                        capturedRequest = request;
                    }

                    return SmtpRuleScriptExecutionResult.Continue(request.MessageData);
                }));

        await session.RunAsync(stream, CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "535 Authentication failed\r\n");
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual("user@example.test", capturedRequest.Client.Username);
        Assert.IsFalse(capturedRequest.Client.IsAuthenticated);
    }

    [TestMethod]
    public async Task RunAsync_AuthPlainRecordsAutoBanFailureAndDisconnectsWhenThresholdReached()
    {
        var authToken = EncodeAuthPlain("user@example.test", "wrong");
        await using var stream = new DuplexMemoryStream(
            $"EHLO client.example\r\nAUTH PLAIN {authToken}\r\nNOOP\r\n");
        var autoBanRecorder = new CapturingAutoBanRecorder(disconnect: true);
        var session = new SmtpSession(
            new SmtpSessionOptions(),
            messageReceiver: null,
            recipientValidator: null,
            accountAuthenticator: new FakeAccountAuthenticator(),
            eventScriptExecutor: null,
            autoBanLogonFailureRecorder: autoBanRecorder);

        await session.RunAsync(
            stream,
            startTlsStreamProvider: null,
            connectionContext: new SmtpSessionConnectionContext(
                ClientIPAddress: "203.0.113.13",
                ClientPort: 2526,
                SessionId: 43),
            cancellationToken: CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "535 Authentication failed\r\n");
        Assert.IsFalse(output.Contains("250 OK\r\n", StringComparison.Ordinal));
        var failure = autoBanRecorder.Failures.Single();
        Assert.AreEqual(IPAddress.Parse("203.0.113.13"), failure.ClientAddress);
        Assert.AreEqual("user@example.test", failure.Username);
    }

    [TestMethod]
    public async Task RunAsync_WritesSyntaxErrorForInvalidLineTerminator()
    {
        await using var stream = new DuplexMemoryStream("NOOP\n");
        var session = new SmtpSession();

        await session.RunAsync(stream, CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "500 Protocol line ended without CRLF terminator.\r\n");
    }

    private static string EncodeAuthPlain(string username, string password) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat('\0', username, '\0', password)));

    private static string EncodeAuthToken(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private sealed class DuplexMemoryStream : Stream
    {
        private readonly MemoryStream _input;
        private readonly MemoryStream _output;
        private readonly bool _ownsOutput;

        public DuplexMemoryStream(string input)
            : this(input, new MemoryStream(), ownsOutput: true)
        {
        }

        private DuplexMemoryStream(
            string input,
            MemoryStream output,
            bool ownsOutput)
        {
            _input = new MemoryStream(Encoding.ASCII.GetBytes(input));
            _output = output;
            _ownsOutput = ownsOutput;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public string GetOutputText() => Encoding.ASCII.GetString(_output.ToArray());

        public DuplexMemoryStream CreateContinuation(string input) =>
            new(input, _output, ownsOutput: false);

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _input.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_input.Read(buffer.Span));
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => _output.Write(buffer, offset, count);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _output.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _input.Dispose();
                if (_ownsOutput)
                {
                    _output.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }

    private sealed class FakeStartTlsProvider : ISmtpStartTlsStreamProvider
    {
        private readonly Stream _upgradedStream;

        public FakeStartTlsProvider(Stream upgradedStream)
        {
            _upgradedStream = upgradedStream;
        }

        public bool SupportsStartTls => true;

        public int UpgradeCount { get; private set; }

        public ValueTask<Stream> UpgradeToTlsAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UpgradeCount++;
            return ValueTask.FromResult(_upgradedStream);
        }
    }

    private sealed class FakeMessageReceiver : ISmtpMessageReceiver
    {
        public SmtpReceiveRequest? LastRequest { get; private set; }

        public ValueTask<SmtpReceiveResult> ReceiveAsync(
            SmtpReceiveRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return ValueTask.FromResult(SmtpReceiveResult.Success());
        }
    }

    private sealed class FakeEventScriptExecutor : ISmtpEventScriptExecutor
    {
        private readonly Func<SmtpEventScriptExecutionRequest, SmtpRuleScriptExecutionResult> _execute;

        public FakeEventScriptExecutor(Func<SmtpEventScriptExecutionRequest, SmtpRuleScriptExecutionResult> execute)
        {
            _execute = execute;
        }

        public SmtpRuleScriptExecutionResult Execute(
            SmtpEventScriptExecutionRequest request,
            CancellationToken cancellationToken) =>
            _execute(request);
    }

    private sealed class FakeRecipientValidator : ISmtpRecipientValidator
    {
        private readonly SmtpRecipientValidationResult _result;

        public FakeRecipientValidator(SmtpRecipientValidationResult result)
        {
            _result = result;
        }

        public ValueTask<SmtpRecipientValidationResult> ValidateAsync(
            SmtpRecipientValidationRequest request,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_result);
    }

    private sealed class AuthenticatedRelayValidator : ISmtpRecipientValidator
    {
        public SmtpRecipientValidationRequest? LastRequest { get; private set; }

        public ValueTask<SmtpRecipientValidationResult> ValidateAsync(
            SmtpRecipientValidationRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return ValueTask.FromResult(
                request.SenderAuthenticated
                    ? SmtpRecipientValidationResult.Accept(
                        new SmtpResolvedRecipient(
                            request.RecipientAddress,
                            request.RecipientAddress,
                            LocalAccountId: 0,
                            IsLocal: false))
                    : SmtpRecipientValidationResult.Reject("550 Relay not permitted"));
        }
    }

    private sealed class FakeAccountAuthenticator : IImapAccountAuthenticator
    {
        public ValueTask<ImapAuthenticationResult> AuthenticateAsync(
            string username,
            string password,
            CancellationToken cancellationToken)
        {
            if (username == "user@example.test" && password == "secret")
            {
                return ValueTask.FromResult(
                    ImapAuthenticationResult.Success(new ImapAuthenticatedAccount(77, username)));
            }

            return ValueTask.FromResult(ImapAuthenticationResult.Failure("Invalid user name or password."));
        }
    }

    private sealed class CapturingAutoBanRecorder : IAutoBanLogonFailureRecorder
    {
        private readonly bool _disconnect;

        public CapturingAutoBanRecorder(bool disconnect)
        {
            _disconnect = disconnect;
        }

        public List<(IPAddress ClientAddress, string Username)> Failures { get; } = [];

        public ValueTask<AutoBanLogonFailureResult> RecordFailureAsync(
            IPAddress clientAddress,
            string username,
            CancellationToken cancellationToken)
        {
            Failures.Add((clientAddress, username));
            return ValueTask.FromResult(
                new AutoBanLogonFailureResult(
                    Enabled: true,
                    FailureCount: Failures.Count,
                    Disconnect: _disconnect,
                    RangeCreated: _disconnect));
        }

        public ValueTask ClearOldFailuresAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
