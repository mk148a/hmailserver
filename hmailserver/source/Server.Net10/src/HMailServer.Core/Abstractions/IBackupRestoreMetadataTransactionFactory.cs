namespace HMailServer.Core.Abstractions;

public interface IBackupRestoreMetadataTransactionFactory
{
    ValueTask<IBackupRestoreMetadataTransaction> BeginAsync(
        CancellationToken cancellationToken);
}

public interface IBackupRestoreMetadataTransaction : IAsyncDisposable
{
    IDomainAdministrationStore DomainStore { get; }

    IAccountAdministrationStore AccountStore { get; }

    IAliasAdministrationStore AliasStore { get; }

    IDistributionListAdministrationStore DistributionListStore { get; }

    IDistributionListRecipientAdministrationStore RecipientStore { get; }

    ISettingsRestoreAdministrationStore? SettingsStore => null;

    IFetchAccountAdministrationStore? FetchAccountStore => null;

    IRuleAdministrationStore? RuleStore => null;

    IRuleCriteriaAdministrationStore? RuleCriteriaStore => null;

    IRuleActionAdministrationStore? RuleActionStore => null;

    IImapFolderAdministrationRestoreStore? FolderRestoreStore => null;

    IImapFolderPermissionAdministrationRestoreStore? FolderPermissionRestoreStore => null;

    IMessageAdministrationRestoreStore? MessageRestoreStore => null;

    IGroupAdministrationStore? GroupStore => null;

    IGroupMemberAdministrationStore? GroupMemberStore => null;

    ISecurityRangeAdministrationStore? SecurityRangeStore => null;

    ITcpIpPortAdministrationStore? TcpIpPortStore => null;

    ValueTask DeleteAllDomainsForRestoreAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Transaction-scoped domain deletion for restore is not implemented by this transaction.");

    ValueTask DeleteAllPublicFoldersForRestoreAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Transaction-scoped public-folder deletion for restore is not implemented by this transaction.");

    ValueTask DeleteAllGroupsForRestoreAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Transaction-scoped group deletion for restore is not implemented by this transaction.");

    ValueTask DeleteAllSecurityRangesForRestoreAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Transaction-scoped security-range deletion for restore is not implemented by this transaction.");

    ValueTask DeleteAllTcpIpPortsForRestoreAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Transaction-scoped TCP/IP port deletion for restore is not implemented by this transaction.");

    ValueTask<IReadOnlyList<ImapFolderAdministrationDeletedMessage>>
        DeleteAllPublicFoldersForRestoreWithManifestAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Transaction-scoped public-folder deletion with a restore manifest is not implemented by this transaction.");

    ValueTask CommitAsync(CancellationToken cancellationToken);
}
