using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using Microsoft.Extensions.Hosting;
using ScriptingComClass = HMailServer.ComInterop.Scripting;

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
                typeof(Database).GUID,
                static () => new Database()),
            new ComLocalServerRegistration(
                typeof(Utilities).GUID,
                static () => new Utilities()),
            new ComLocalServerRegistration(
                typeof(Links).GUID,
                static () => new Links()),
            new ComLocalServerRegistration(
                typeof(Status).GUID,
                static () => new Status()),
            new ComLocalServerRegistration(
                typeof(Settings).GUID,
                static () => new Settings()),
            new ComLocalServerRegistration(
                typeof(Logging).GUID,
                static () => new Logging()),
            new ComLocalServerRegistration(
                typeof(ScriptingComClass).GUID,
                static () => new ScriptingComClass()),
            new ComLocalServerRegistration(
                typeof(BackupSettings).GUID,
                static () => new BackupSettings()),
            new ComLocalServerRegistration(
                typeof(BackupManager).GUID,
                static () => new BackupManager()),
            new ComLocalServerRegistration(
                typeof(Backup).GUID,
                static () => new Backup()),
            new ComLocalServerRegistration(
                typeof(GlobalObjects).GUID,
                static () => new GlobalObjects()),
            new ComLocalServerRegistration(
                typeof(DeliveryQueue).GUID,
                static () => new DeliveryQueue()),
            new ComLocalServerRegistration(
                typeof(Directories).GUID,
                static () => new Directories()),
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
                typeof(Rules).GUID,
                static () => new Rules()),
            new ComLocalServerRegistration(
                typeof(Rule).GUID,
                static () => new Rule()),
            new ComLocalServerRegistration(
                typeof(IMAPFolders).GUID,
                static () => new IMAPFolders()),
            new ComLocalServerRegistration(
                typeof(IMAPFolder).GUID,
                static () => new IMAPFolder()),
            new ComLocalServerRegistration(
                typeof(Routes).GUID,
                static () => new Routes()),
            new ComLocalServerRegistration(
                typeof(Route).GUID,
                static () => new Route()),
            new ComLocalServerRegistration(
                typeof(IncomingRelays).GUID,
                static () => new IncomingRelays()),
            new ComLocalServerRegistration(
                typeof(IncomingRelay).GUID,
                static () => new IncomingRelay()),
            new ComLocalServerRegistration(
                typeof(SecurityRanges).GUID,
                static () => new SecurityRanges()),
            new ComLocalServerRegistration(
                typeof(SecurityRange).GUID,
                static () => new SecurityRange()),
            new ComLocalServerRegistration(
                typeof(ServerMessages).GUID,
                static () => new ServerMessages()),
            new ComLocalServerRegistration(
                typeof(ServerMessage).GUID,
                static () => new ServerMessage()),
            new ComLocalServerRegistration(
                typeof(TCPIPPorts).GUID,
                static () => new TCPIPPorts()),
            new ComLocalServerRegistration(
                typeof(TCPIPPort).GUID,
                static () => new TCPIPPort()),
            new ComLocalServerRegistration(
                typeof(SSLCertificates).GUID,
                static () => new SSLCertificates()),
            new ComLocalServerRegistration(
                typeof(SSLCertificate).GUID,
                static () => new SSLCertificate()),
            new ComLocalServerRegistration(
                typeof(Groups).GUID,
                static () => new Groups()),
            new ComLocalServerRegistration(
                typeof(Group).GUID,
                static () => new Group()),
            new ComLocalServerRegistration(
                typeof(GroupMembers).GUID,
                static () => new GroupMembers()),
            new ComLocalServerRegistration(
                typeof(GroupMember).GUID,
                static () => new GroupMember()),
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
