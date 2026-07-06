using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using Microsoft.Extensions.Hosting;
using ScriptingComClass = HMailServer.ComInterop.Scripting;

namespace HMailServer.Service;

internal sealed class ComLocalServerHostedService : IHostedService, IDisposable
{
    private readonly ComLocalServerHost _host;

    public ComLocalServerHostedService(
        IServerAdministratorAuthenticationProvider authenticationProvider,
        ILegacyBlowfishCipher legacyBlowfishCipher,
        ILocalHostRuntime localHostRuntime,
        IMailServerResolver mailServerResolver,
        IMessageIdResolver messageIdResolver,
        IImapFolderUidMaintenanceStore imapFolderUidMaintenanceStore,
        IServiceDependencyRuntime serviceDependencyRuntime,
        IEmailAllAccountsRuntime emailAllAccountsRuntime)
    {
        ArgumentNullException.ThrowIfNull(authenticationProvider);
        ArgumentNullException.ThrowIfNull(legacyBlowfishCipher);
        ArgumentNullException.ThrowIfNull(localHostRuntime);
        ArgumentNullException.ThrowIfNull(mailServerResolver);
        ArgumentNullException.ThrowIfNull(messageIdResolver);
        ArgumentNullException.ThrowIfNull(imapFolderUidMaintenanceStore);
        ArgumentNullException.ThrowIfNull(serviceDependencyRuntime);
        ArgumentNullException.ThrowIfNull(emailAllAccountsRuntime);

        _host = new ComLocalServerHost(
            new ComLocalServerRegistration(
                typeof(Application).GUID,
                () => Application.CreateForRuntime(
                    authenticationProvider,
                    legacyBlowfishCipher,
                    localHostRuntime,
                    mailServerResolver,
                    messageIdResolver,
                    imapFolderUidMaintenanceStore,
                    serviceDependencyRuntime,
                    emailAllAccountsRuntime)),
            new ComLocalServerRegistration(
                typeof(Database).GUID,
                static () => new Database()),
            new ComLocalServerRegistration(
                typeof(Utilities).GUID,
                () => Utilities.CreateForRuntime(
                    legacyBlowfishCipher,
                    localHostRuntime,
                    mailServerResolver,
                    messageIdResolver,
                    imapFolderUidMaintenanceStore,
                    serviceDependencyRuntime,
                    emailAllAccountsRuntime)),
            new ComLocalServerRegistration(
                typeof(Links).GUID,
                static () => new Links()),
            new ComLocalServerRegistration(
                typeof(DiagnosticResults).GUID,
                static () => new DiagnosticResults()),
            new ComLocalServerRegistration(
                typeof(DiagnosticResult).GUID,
                static () => new DiagnosticResult()),
            new ComLocalServerRegistration(
                typeof(Diagnostics).GUID,
                static () => new Diagnostics()),
            new ComLocalServerRegistration(
                typeof(Status).GUID,
                static () => new Status()),
            new ComLocalServerRegistration(
                typeof(Settings).GUID,
                static () => new Settings()),
            new ComLocalServerRegistration(
                typeof(Cache).GUID,
                static () => new Cache()),
            new ComLocalServerRegistration(
                typeof(AntiVirus).GUID,
                static () => new AntiVirus()),
            new ComLocalServerRegistration(
                typeof(AntiSpam).GUID,
                static () => new AntiSpam()),
            new ComLocalServerRegistration(
                typeof(BlockedAttachments).GUID,
                static () => new BlockedAttachments()),
            new ComLocalServerRegistration(
                typeof(BlockedAttachment).GUID,
                static () => new BlockedAttachment()),
            new ComLocalServerRegistration(
                typeof(DNSBlackLists).GUID,
                static () => new DNSBlackLists()),
            new ComLocalServerRegistration(
                typeof(DNSBlackList).GUID,
                static () => new DNSBlackList()),
            new ComLocalServerRegistration(
                typeof(SURBLServers).GUID,
                static () => new SURBLServers()),
            new ComLocalServerRegistration(
                typeof(SURBLServer).GUID,
                static () => new SURBLServer()),
            new ComLocalServerRegistration(
                typeof(GreyListingWhiteAddresses).GUID,
                static () => new GreyListingWhiteAddresses()),
            new ComLocalServerRegistration(
                typeof(GreyListingWhiteAddress).GUID,
                static () => new GreyListingWhiteAddress()),
            new ComLocalServerRegistration(
                typeof(WhiteListAddresses).GUID,
                static () => new WhiteListAddresses()),
            new ComLocalServerRegistration(
                typeof(WhiteListAddress).GUID,
                static () => new WhiteListAddress()),
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
                typeof(Language).GUID,
                static () => new Language()),
            new ComLocalServerRegistration(
                typeof(Languages).GUID,
                static () => new Languages()),
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
                typeof(Messages).GUID,
                static () => new Messages()),
            new ComLocalServerRegistration(
                typeof(Message).GUID,
                static () => new Message()),
            new ComLocalServerRegistration(
                typeof(Attachments).GUID,
                static () => new Attachments()),
            new ComLocalServerRegistration(
                typeof(Attachment).GUID,
                static () => new Attachment()),
            new ComLocalServerRegistration(
                typeof(Recipients).GUID,
                static () => new Recipients()),
            new ComLocalServerRegistration(
                typeof(Recipient).GUID,
                static () => new Recipient()),
            new ComLocalServerRegistration(
                typeof(MessageHeaders).GUID,
                static () => new MessageHeaders()),
            new ComLocalServerRegistration(
                typeof(MessageHeader).GUID,
                static () => new MessageHeader()),
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
                typeof(RuleCriterias).GUID,
                static () => new RuleCriterias()),
            new ComLocalServerRegistration(
                typeof(RuleCriteria).GUID,
                static () => new RuleCriteria()),
            new ComLocalServerRegistration(
                typeof(RuleActions).GUID,
                static () => new RuleActions()),
            new ComLocalServerRegistration(
                typeof(RuleAction).GUID,
                static () => new RuleAction()),
            new ComLocalServerRegistration(
                typeof(IMAPFolders).GUID,
                static () => new IMAPFolders()),
            new ComLocalServerRegistration(
                typeof(IMAPFolder).GUID,
                static () => new IMAPFolder()),
            new ComLocalServerRegistration(
                typeof(IMAPFolderPermissions).GUID,
                static () => new IMAPFolderPermissions()),
            new ComLocalServerRegistration(
                typeof(IMAPFolderPermission).GUID,
                static () => new IMAPFolderPermission()),
            new ComLocalServerRegistration(
                typeof(Routes).GUID,
                static () => new Routes()),
            new ComLocalServerRegistration(
                typeof(Route).GUID,
                static () => new Route()),
            new ComLocalServerRegistration(
                typeof(RouteAddresses).GUID,
                static () => new RouteAddresses()),
            new ComLocalServerRegistration(
                typeof(RouteAddress).GUID,
                static () => new RouteAddress()),
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
