namespace HMailServer.Core.Abstractions;

public interface IBackupDomainProjectionSnapshotFactory
{
    ValueTask<IBackupDomainProjectionSnapshot> BeginAsync(
        CancellationToken cancellationToken);
}

public interface IBackupDomainProjectionSnapshot : IAsyncDisposable
{
    ISettingsAdministrationStore SettingsStore { get; }

    IBackupSettingsPropertyStore BackupSettingsPropertyStore { get; }

    ISecurityRangeAdministrationStore SecurityRangeStore { get; }

    ITcpIpPortAdministrationStore TcpIpPortStore { get; }

    IBlockedAttachmentAdministrationStore BlockedAttachmentStore { get; }

    ISurblServerAdministrationStore SurblServerStore { get; }

    IGroupAdministrationStore GroupStore { get; }

    IGroupMemberAdministrationStore GroupMemberStore { get; }

    IDomainAdministrationStore DomainStore { get; }

    IAccountAdministrationStore AccountStore { get; }

    IBackupAccountAdministrationStore BackupAccountStore { get; }

    IBackupFetchAccountAdministrationStore BackupFetchAccountStore { get; }

    IBackupRuleAdministrationStore BackupRuleStore { get; }

    IRuleCriteriaAdministrationStore RuleCriteriaStore { get; }

    IRuleActionAdministrationStore RuleActionStore { get; }

    IImapFolderAdministrationStore FolderStore { get; }

    IMessageAdministrationBackupStore MessageBackupStore { get; }

    IDomainAliasAdministrationStore DomainAliasStore { get; }

    IAliasAdministrationStore AliasStore { get; }

    IDistributionListAdministrationStore DistributionListStore { get; }

    IDistributionListRecipientAdministrationStore RecipientStore { get; }
}
