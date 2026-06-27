using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using Microsoft.Extensions.Hosting;

namespace HMailServer.Service;

internal sealed class ComLocalServerHostedService : IHostedService, IDisposable
{
    private readonly ComLocalServerHost _host;

    public ComLocalServerHostedService(IServerAdministratorAuthenticationProvider authenticationProvider)
    {
        ArgumentNullException.ThrowIfNull(authenticationProvider);

        _host = new ComLocalServerHost(
            new ComLocalServerRegistration(
                typeof(Application).GUID,
                () => Application.CreateForRuntime(authenticationProvider)),
            new ComLocalServerRegistration(
                typeof(Settings).GUID,
                static () => new Settings()),
            new ComLocalServerRegistration(
                typeof(Domains).GUID,
                static () => new Domains()),
            new ComLocalServerRegistration(
                typeof(Domain).GUID,
                static () => new Domain()),
            new ComLocalServerRegistration(
                typeof(Accounts).GUID,
                static () => new Accounts()),
            new ComLocalServerRegistration(
                typeof(Account).GUID,
                static () => new Account()),
            new ComLocalServerRegistration(
                typeof(FetchAccount).GUID,
                static () => new FetchAccount()),
            new ComLocalServerRegistration(
                typeof(FetchAccounts).GUID,
                static () => new FetchAccounts()),
            new ComLocalServerRegistration(
                typeof(Aliases).GUID,
                static () => new Aliases()),
            new ComLocalServerRegistration(
                typeof(Alias).GUID,
                static () => new Alias()),
            new ComLocalServerRegistration(
                typeof(DistributionLists).GUID,
                static () => new DistributionLists()),
            new ComLocalServerRegistration(
                typeof(DistributionList).GUID,
                static () => new DistributionList()),
            new ComLocalServerRegistration(
                typeof(DistributionListRecipients).GUID,
                static () => new DistributionListRecipients()),
            new ComLocalServerRegistration(
                typeof(DistributionListRecipient).GUID,
                static () => new DistributionListRecipient()),
            new ComLocalServerRegistration(
                typeof(DomainAliases).GUID,
                static () => new DomainAliases()),
            new ComLocalServerRegistration(
                typeof(DomainAlias).GUID,
                static () => new DomainAlias()),
            new ComLocalServerRegistration(
                typeof(MessageIndexing).GUID,
                static () => new MessageIndexing()));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _host.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _host.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose() => _host.Dispose();
}
