using HMailServer.Service;
using System.Net;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using HMailServer.Indexing;
using HMailServer.Protocols;
using HMailServer.Protocols.Imap;
using HMailServer.Protocols.Pop3;
using HMailServer.Protocols.Smtp;
using HMailServer.Scripting;
using HMailServer.Search.SqlServer;
using HMailServer.Security;
using HMailServer.Storage.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;

if (TryHandleComRegistrationCommand(args))
{
    return;
}

var hostComposition = HMailServer.Service.Host.Build(args);
var host = hostComposition.Host;
var dataDirectory = hostComposition.DataDirectory;
BackupRestoreRecoveryJournal.EnsureNoPendingRecovery(dataDirectory);
var backupMessagesDbOnly = hostComposition.BackupMessagesDbOnly;
var userInterfaceLanguage = hostComposition.UserInterfaceLanguage;
var rewriteEnvelopeFromWhenForwarding = hostComposition.RewriteEnvelopeFromWhenForwarding;
var reinitializationCoordinator =
    host.Services.GetRequiredService<ServiceReinitializationCoordinator>();

var directoryAdministrationStore = host.Services.GetRequiredService<IDirectoryAdministrationStore>();
var directoryAdministrationSnapshot = await directoryAdministrationStore
    .GetDirectoriesAsync(CancellationToken.None);
ApplicationRuntimeHost.Configure(
    host.Services.GetRequiredService<IApplicationRuntimeStore>());
BackupManagerRuntimeHost.Configure(
    new BackupOperationRuntime(
        host.Services.GetRequiredService<IBackupTaskQueue>(),
        new BackupStartPlanRuntime(
            host.Services.GetRequiredService<ISettingsAdministrationStore>(),
            host.Services.GetRequiredService<IBackupPreflightAdministrationStore>(),
            dataDirectory,
            backupMessagesDbOnly,
            backupSettingsPropertyStore:
                host.Services.GetRequiredService<IBackupSettingsPropertyStore>())
            .GetEvidenceAsync,
        new SevenZipBackupArchiveRuntime(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            host.Services
                .GetRequiredService<IApplicationRuntimeStore>()
                .GetSnapshot()
                .Version,
            payloadProvider: new BackupXmlPayloadRuntime(
                host.Services.GetRequiredService<ISettingsAdministrationStore>(),
                host.Services.GetRequiredService<IDomainAdministrationStore>(),
                host.Services.GetRequiredService<IDomainAliasAdministrationStore>(),
                host.Services.GetRequiredService<IAccountAdministrationStore>(),
                host.Services.GetRequiredService<IAliasAdministrationStore>(),
                host.Services.GetRequiredService<IDistributionListAdministrationStore>(),
                host.Services.GetRequiredService<IDistributionListRecipientAdministrationStore>(),
                backupAccountStore: host.Services.GetRequiredService<IBackupAccountAdministrationStore>(),
                fetchAccountStore: host.Services.GetRequiredService<IFetchAccountAdministrationStore>(),
                backupFetchAccountStore: host.Services.GetRequiredService<IBackupFetchAccountAdministrationStore>(),
                backupRuleStore: host.Services.GetRequiredService<IBackupRuleAdministrationStore>(),
                ruleStore: host.Services.GetRequiredService<IRuleAdministrationStore>(),
                ruleCriteriaStore: host.Services.GetRequiredService<IRuleCriteriaAdministrationStore>(),
                ruleActionStore: host.Services.GetRequiredService<IRuleActionAdministrationStore>(),
                folderStore: host.Services.GetRequiredService<IImapFolderAdministrationStore>(),
                folderRestoreStore: host.Services.GetRequiredService<IImapFolderAdministrationRestoreStore>(),
                messageStore: host.Services.GetRequiredService<IMessageAdministrationStore>(),
                 metadataTransactionFactory: host.Services
                     .GetRequiredService<IBackupRestoreMetadataTransactionFactory>(),
                 requireSqlTransaction: true,
                 groupStore: host.Services.GetRequiredService<IGroupAdministrationStore>(),
                 groupMemberStore: host.Services.GetRequiredService<IGroupMemberAdministrationStore>())
                .GetPayloadAsync,
             dataDirectory: dataDirectory,
             restoreReinitializer: reinitializationCoordinator.ReinitializeAsync)
             .CreateAsync));
if (host.Services.GetService<IBackupEventScriptExecutor>() is { } backupEventExecutor)
{
    BackupEventDispatcherRuntimeHost.Configure(backupEventExecutor);
}
MessageIndexingRuntimeHost.Configure(
    host.Services.GetRequiredService<StoreBackedMessageIndexingRuntime>());
DatabaseAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IDatabaseAdministrationStore>(),
    host.Services.GetRequiredService<IMessageFileNameLookup>());
StatusAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IServerStatusAdministrationStore>());
DeliveryQueueAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IDeliveryQueueAdministrationStore>(),
    host.Services.GetRequiredService<IDeliveryQueueWakeSignal>(),
    host.Services.GetRequiredService<IDeliveryQueueClearCoordinator>());
LanguageAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<ILanguageAdministrationStore>());
SettingsAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<ISettingsAdministrationStore>(),
    new SettingsRuntimeConfiguration(
        UserInterfaceLanguage: userInterfaceLanguage,
        RewriteEnvelopeFromWhenForwarding: rewriteEnvelopeFromWhenForwarding,
        LoggingDirectory: directoryAdministrationSnapshot.LogDirectory,
        ScriptingDirectory: directoryAdministrationSnapshot.EventDirectory,
        ScriptSyntaxChecker: host.Services.GetRequiredService<IScriptSyntaxChecker>(),
        ScriptRuntimeReloader: host.Services.GetRequiredService<IScriptRuntimeReloader>(),
        ClamAvScannerTestRuntime: host.Services.GetRequiredService<IClamAvScannerTestRuntime>(),
        ClamWinScannerTestRuntime: new ClamWinScannerTestRuntime(
            new ClamWinScannerTestRuntimeOptions
            {
                DataDirectory = directoryAdministrationSnapshot.DataDirectory,
                TempDirectory = directoryAdministrationSnapshot.TempDirectory
            }),
        CustomScannerTestRuntime: new CustomScannerTestRuntime(
            new CustomScannerTestRuntimeOptions
            {
                DataDirectory = directoryAdministrationSnapshot.DataDirectory
            }),
        DkimVerificationRuntime: host.Services.GetRequiredService<IDkimVerificationRuntime>(),
        GreyListingTripletAdministrationStore:
            host.Services.GetRequiredService<IGreyListingTripletAdministrationStore>(),
        GreyListingEnabledPublisher: value =>
            host.Services.GetRequiredService<SmtpGreylistingOptions>().Enabled = value,
        GreyListingInitialDelayPublisher: value =>
            host.Services.GetRequiredService<SmtpGreylistingOptions>().InitialDelay = TimeSpan.FromMinutes(value),
        GreyListingInitialDeletePublisher: value =>
            host.Services.GetRequiredService<SmtpGreylistingOptions>().InitialRecordLifetime = TimeSpan.FromHours(value),
        SpamAssassinConnectionTestRuntime:
            host.Services.GetRequiredService<ISpamAssassinConnectionTestRuntime>(),
        LogonFailureAdministrationStore:
            host.Services.GetRequiredService<ILogonFailureAdministrationStore>()));
BlockedAttachmentAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IBlockedAttachmentAdministrationStore>());
DnsBlackListAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IDnsBlackListAdministrationStore>());
SurblServerAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<ISurblServerAdministrationStore>());
GreyListingWhiteAddressAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IGreyListingWhiteAddressAdministrationStore>());
WhiteListAddressAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IWhiteListAddressAdministrationStore>());
DomainAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IDomainAdministrationStore>());
AccountAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IAccountAdministrationStore>(),
    host.Services.GetRequiredService<IPop3MailboxLockManager>().Unlock,
    new SqlServerAccountPasswordVerifier(
        host.Services.GetRequiredService<SqlServerConnectionFactory>(),
        host.Services.GetRequiredService<IAccountAdministrationStore>(),
        host.Services.GetService<IClientPasswordValidationScriptExecutor>()).Verify);
MessageAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IMessageAdministrationStore>(),
    host.Services.GetRequiredService<IMessageAdministrationContentSource>());
FetchAccountAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IFetchAccountAdministrationStore>(),
    host.Services.GetRequiredService<IExternalFetchWakeSignal>());
RuleAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IRuleAdministrationStore>());
RuleCriteriaAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IRuleCriteriaAdministrationStore>());
RuleActionAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IRuleActionAdministrationStore>());
ImapFolderAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IImapFolderAdministrationStore>(),
    host.Services.GetRequiredService<MessageFileDeletionRuntime>());
RouteAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IRouteAdministrationStore>());
RouteAddressAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IRouteAddressAdministrationStore>());
IncomingRelayAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IIncomingRelayAdministrationStore>());
SecurityRangeAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<ISecurityRangeAdministrationStore>());
TcpIpPortAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<ITcpIpPortAdministrationStore>());
SslCertificateAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<ISslCertificateAdministrationStore>());
ServerMessageAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IServerMessageAdministrationStore>());
DirectoryAdministrationRuntimeHost.Configure(
    directoryAdministrationStore);
GroupAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IGroupAdministrationStore>());
GroupMemberAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IGroupMemberAdministrationStore>());
AliasAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IAliasAdministrationStore>());
DistributionListAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IDistributionListAdministrationStore>());
DistributionListRecipientAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IDistributionListRecipientAdministrationStore>());
DomainAliasAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IDomainAliasAdministrationStore>());
LinksAdministrationRuntimeHost.Configure(
    host.Services.GetRequiredService<IDomainAdministrationStore>(),
    host.Services.GetRequiredService<IAccountAdministrationStore>(),
    host.Services.GetRequiredService<IAliasAdministrationStore>(),
    host.Services.GetRequiredService<IDistributionListAdministrationStore>());
await host.RunAsync().ConfigureAwait(false);

static bool TryHandleComRegistrationCommand(string[] arguments)
{
    if (arguments.Length != 1)
    {
        return false;
    }

    var executablePath = Path.Combine(AppContext.BaseDirectory, "hMailServer.exe");
    var typeLibraryPath = Path.Combine(AppContext.BaseDirectory, "hMailServer.tlb");
    if (arguments[0].Equals("--register-com", StringComparison.OrdinalIgnoreCase)
        || arguments[0].Equals("/RegisterTypeLib", StringComparison.OrdinalIgnoreCase))
    {
        WindowsComRegistration.Register(executablePath, typeLibraryPath);
        return true;
    }

    if (arguments[0].Equals("--unregister-com", StringComparison.OrdinalIgnoreCase))
    {
        WindowsComRegistration.Unregister(executablePath, typeLibraryPath);
        return true;
    }

    return false;
}
