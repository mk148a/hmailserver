using System.Net;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ClientAwareAuthenticationServiceTests
{
    [TestMethod]
    public async Task SuccessfulAuthenticationDoesNotRecordFailure()
    {
        var account = new ImapAuthenticatedAccount(7, "user@example.test");
        var authenticator = new FakeAuthenticator(ImapAuthenticationResult.Success(account));
        var recorder = new CapturingRecorder(new AutoBanLogonFailureResult(
            Enabled: true,
            FailureCount: 1,
            Disconnect: true,
            RangeCreated: false));
        var service = new ClientAwareAuthenticationService(authenticator, recorder);

        var result = await service.AuthenticateAsync(
            new ClientAuthenticationRequest(
                "user@example.test",
                "secret",
                IPAddress.Parse("192.0.2.44"),
                ClientAuthenticationCaller.Smtp),
            CancellationToken.None);

        Assert.IsTrue(result.Authentication.Succeeded);
        Assert.IsFalse(result.Disconnect);
        Assert.AreEqual(0, recorder.CallCount);
        Assert.AreEqual("user@example.test", authenticator.Username);
        Assert.AreEqual("secret", authenticator.Password);
    }

    [TestMethod]
    public async Task FailedAuthenticationRecordsClientAddressAndDisconnectDecision()
    {
        var authenticator = new FakeAuthenticator(ImapAuthenticationResult.Failure("Invalid user name or password."));
        var recorder = new CapturingRecorder(new AutoBanLogonFailureResult(
            Enabled: true,
            FailureCount: 3,
            Disconnect: true,
            RangeCreated: true));
        var service = new ClientAwareAuthenticationService(authenticator, recorder);

        var result = await service.AuthenticateAsync(
            new ClientAuthenticationRequest(
                "user@example.test",
                "wrong",
                IPAddress.Parse("198.51.100.27"),
                ClientAuthenticationCaller.Imap),
            CancellationToken.None);

        Assert.IsFalse(result.Authentication.Succeeded);
        Assert.IsTrue(result.Disconnect);
        Assert.AreEqual(1, recorder.CallCount);
        Assert.AreEqual(IPAddress.Parse("198.51.100.27"), recorder.ClientAddress);
        Assert.AreEqual("user@example.test", recorder.Username);
    }

    [TestMethod]
    public async Task MissingClientAddressSkipsFailureRecording()
    {
        var authenticator = new FakeAuthenticator(ImapAuthenticationResult.Failure("Invalid user name or password."));
        var recorder = new CapturingRecorder(new AutoBanLogonFailureResult(
            Enabled: true,
            FailureCount: 3,
            Disconnect: true,
            RangeCreated: true));
        var service = new ClientAwareAuthenticationService(authenticator, recorder);

        var result = await service.AuthenticateAsync(
            new ClientAuthenticationRequest(
                "user@example.test",
                "wrong",
                ClientAddress: null,
                ClientAuthenticationCaller.Pop3),
            CancellationToken.None);

        Assert.IsFalse(result.Authentication.Succeeded);
        Assert.IsFalse(result.Disconnect);
        Assert.AreEqual(0, recorder.CallCount);
    }

    [TestMethod]
    public async Task RecorderFailureDoesNotChangeAuthenticationFailureResult()
    {
        var authenticator = new FakeAuthenticator(ImapAuthenticationResult.Failure("Invalid user name or password."));
        var recorder = new CapturingRecorder(
            new InvalidOperationException("store unavailable"));
        var service = new ClientAwareAuthenticationService(authenticator, recorder);

        var result = await service.AuthenticateAsync(
            new ClientAuthenticationRequest(
                "user@example.test",
                "wrong",
                IPAddress.Parse("203.0.113.19"),
                ClientAuthenticationCaller.Pop3),
            CancellationToken.None);

        Assert.IsFalse(result.Authentication.Succeeded);
        Assert.IsFalse(result.Disconnect);
        Assert.AreEqual(1, recorder.CallCount);
    }

    [TestMethod]
    public async Task SuccessfulFlagWithoutAccountStillRecordsFailure()
    {
        var authenticator = new FakeAuthenticator(new ImapAuthenticationResult(
            Succeeded: true,
            Account: null,
            FailureMessage: string.Empty));
        var recorder = new CapturingRecorder(new AutoBanLogonFailureResult(
            Enabled: true,
            FailureCount: 3,
            Disconnect: true,
            RangeCreated: true));
        var service = new ClientAwareAuthenticationService(authenticator, recorder);

        var result = await service.AuthenticateAsync(
            new ClientAuthenticationRequest(
                "user@example.test",
                "wrong",
                IPAddress.Parse("203.0.113.19"),
                ClientAuthenticationCaller.Imap),
            CancellationToken.None);

        Assert.IsTrue(result.Authentication.Succeeded);
        Assert.IsNull(result.Authentication.Account);
        Assert.IsTrue(result.Disconnect);
        Assert.AreEqual(1, recorder.CallCount);
    }

    private sealed class FakeAuthenticator : IImapAccountAuthenticator
    {
        private readonly ImapAuthenticationResult _result;

        public FakeAuthenticator(ImapAuthenticationResult result)
        {
            _result = result;
        }

        public string? Username { get; private set; }

        public string? Password { get; private set; }

        public ValueTask<ImapAuthenticationResult> AuthenticateAsync(
            string username,
            string password,
            CancellationToken cancellationToken)
        {
            Username = username;
            Password = password;
            return ValueTask.FromResult(_result);
        }
    }

    private sealed class CapturingRecorder : IAutoBanLogonFailureRecorder
    {
        private readonly AutoBanLogonFailureResult? _result;
        private readonly Exception? _exception;

        public CapturingRecorder(AutoBanLogonFailureResult result)
        {
            _result = result;
        }

        public CapturingRecorder(Exception exception)
        {
            _exception = exception;
        }

        public int CallCount { get; private set; }

        public IPAddress? ClientAddress { get; private set; }

        public string? Username { get; private set; }

        public ValueTask<AutoBanLogonFailureResult> RecordFailureAsync(
            IPAddress clientAddress,
            string username,
            CancellationToken cancellationToken)
        {
            CallCount++;
            ClientAddress = clientAddress;
            Username = username;
            if (_exception is not null)
            {
                throw _exception;
            }

            return ValueTask.FromResult(_result!);
        }

        public ValueTask ClearOldFailuresAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }
}
