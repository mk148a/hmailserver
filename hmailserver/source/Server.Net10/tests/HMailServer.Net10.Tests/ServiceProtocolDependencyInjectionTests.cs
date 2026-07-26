using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols;
using HMailServer.Protocols.Imap;
using HMailServer.Protocols.Pop3;
using HMailServer.Protocols.Smtp;
using Microsoft.Extensions.DependencyInjection;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ServiceProtocolDependencyInjectionTests
{
    [TestMethod]
    public void CallerAwareProtocolServices_ConstructWithoutDatabaseOrDataDirectory()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IImapAccountAuthenticator, FakeAccountAuthenticator>();
        services.AddSingleton<IAutoBanLogonFailureRecorder, NoOpAutoBanLogonFailureRecorder>();
        services.AddSingleton<IMessageSearchIndex, NoOpMessageSearchIndex>();
        services.AddSingleton<IPop3MailboxStore, NoOpPop3MailboxStore>();
        services.AddSingleton(new ImapTcpListenerOptions
        {
            ListenAddress = IPAddress.Loopback,
            Port = 0,
            Backlog = 16,
            MaxConcurrentConnections = 1
        });
        services.AddSingleton(new Pop3TcpListenerOptions
        {
            ListenAddress = IPAddress.Loopback,
            Port = 0,
            Backlog = 16,
            MaxConcurrentConnections = 1
        });
        services.AddSingleton(new SmtpTcpListenerOptions
        {
            ListenAddress = IPAddress.Loopback,
            Port = 0,
            Backlog = 16,
            MaxConcurrentConnections = 1
        });
        services.AddSingleton<IImapSessionContextProvider>(
            new FixedImapSessionContextProvider(new ImapSessionContext()));
        services.AddSingleton<IImapConnectionStreamFactory, PlainImapConnectionStreamFactory>();
        services.AddSingleton<IPop3ConnectionStreamFactory, PlainPop3ConnectionStreamFactory>();
        services.AddSingleton<ISmtpConnectionStreamFactory, PlainSmtpConnectionStreamFactory>();
        services.AddCallerAwareProtocolServices();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var authenticationService = provider.GetRequiredService<IClientAwareAuthenticationService>();

        Assert.IsInstanceOfType(authenticationService, typeof(ClientAwareAuthenticationService));
        Assert.IsNotNull(provider.GetRequiredService<ImapSession>());
        Assert.IsNotNull(provider.GetRequiredService<Pop3Session>());
        Assert.IsNotNull(provider.GetRequiredService<SmtpSession>());
        Assert.IsNotNull(provider.GetRequiredService<ImapTcpListener>());
        Assert.IsNotNull(provider.GetRequiredService<Pop3TcpListener>());
        Assert.IsNotNull(provider.GetRequiredService<SmtpTcpListener>());
    }

    private sealed class FakeAccountAuthenticator : IImapAccountAuthenticator
    {
        public ValueTask<ImapAuthenticationResult> AuthenticateAsync(
            string username,
            string password,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(ImapAuthenticationResult.Failure("not used"));
    }

    private sealed class NoOpAutoBanLogonFailureRecorder : IAutoBanLogonFailureRecorder
    {
        public ValueTask<AutoBanLogonFailureResult> RecordFailureAsync(
            IPAddress clientAddress,
            string username,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AutoBanLogonFailureResult(false, 0, false, false));

        public ValueTask ClearOldFailuresAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }

    private sealed class NoOpMessageSearchIndex : IMessageSearchIndex
    {
        public ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(true);

        public ValueTask QueueForIndexingAsync(
            MessageIdentity identity,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask UpsertAsync(
            MessageSearchDocument document,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public async IAsyncEnumerable<MessageIdentity> SearchAsync(
            ImapSearchRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class NoOpPop3MailboxStore : IPop3MailboxStore
    {
        public ValueTask<IReadOnlyList<Pop3MessageListing>> ListMessagesAsync(
            ImapAuthenticatedAccount account,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<Pop3MessageListing>>([]);

        public ValueTask<Stream> OpenMessageAsync(
            ImapAuthenticatedAccount account,
            long messageId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<Stream>(new MemoryStream());

        public ValueTask DeleteMessagesAsync(
            ImapAuthenticatedAccount account,
            IReadOnlyCollection<long> messageIds,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }
}
