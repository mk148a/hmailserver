using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(false)]
internal interface IBackupRestoreExecutor
{
    ValueTask ExecuteAsync(Backup backup, CancellationToken cancellationToken);
}

[ComVisible(false)]
internal static class BackupRestoreRuntimeHost
{
    private static IBackupRestoreExecutor? _runtime;

    internal static IBackupRestoreExecutor? Runtime => Volatile.Read(ref _runtime);

    internal static void Configure(IBackupRestoreExecutor runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Volatile.Write(ref _runtime, runtime);
    }

    internal static void ResetForTests() => Volatile.Write(ref _runtime, null);
}

[ComVisible(false)]
internal sealed class MetadataBackupRestoreExecutor : IBackupRestoreExecutor
{
    private const int SupportedDbOnlyRestoreOptions = BackupStartPlan.BackupDomainsFlag;
    private const int SupportedDbOnlyRestoreOptionsWithSettings =
        BackupStartPlan.BackupSettingsFlag | BackupStartPlan.BackupDomainsFlag;
    private const int SupportedDataRestoreOptions =
        BackupStartPlan.BackupDomainsFlag | BackupStartPlan.BackupMessagesFlag;
    private const int SupportedFullRestoreOptions =
        BackupStartPlan.BackupSettingsFlag
        | BackupStartPlan.BackupDomainsFlag
        | BackupStartPlan.BackupMessagesFlag;

    private readonly BackupRestoreIntegrityRuntime _integrityRuntime;
    private readonly string _dataDirectory;
    private readonly IDomainAdministrationStore _domainStore;
    private readonly IAccountAdministrationStore _accountStore;
    private readonly IAliasAdministrationStore _aliasStore;
    private readonly IDistributionListAdministrationStore _distributionListStore;
    private readonly IDistributionListRecipientAdministrationStore _recipientStore;
    private readonly IGroupAdministrationStore? _groupStore;
    private readonly IGroupMemberAdministrationStore? _groupMemberStore;
    private readonly IFetchAccountAdministrationStore? _fetchAccountStore;
    private readonly IRuleAdministrationStore? _ruleStore;
    private readonly IRuleCriteriaAdministrationStore? _ruleCriteriaStore;
    private readonly IRuleActionAdministrationStore? _ruleActionStore;
    private readonly IImapFolderAdministrationRestoreStore? _folderRestoreStore;
    private readonly IImapFolderAdministrationRestoreDeletionStore? _folderRestoreDeletionStore;
    private readonly IMessageAdministrationRestoreStore? _messageRestoreStore;
    private readonly IMessageAdministrationStore? _messageStore;
    private readonly SevenZipBackupArchiveMetadataReader _metadataReader;
    private readonly BackupRestoreDataDirectoryRuntime _dataDirectoryRuntime;
    private readonly Func<BackupRestoreDataDirectoryBoundary> _dataDirectoryBoundaryFactory;
    private readonly IBackupRestoreMetadataTransactionFactory? _metadataTransactionFactory;
    private readonly bool _requireSqlTransaction;
    private readonly Func<CancellationToken, ValueTask> _reinitialize;
    private readonly bool _reinitializeConfigured;

    internal bool ReinitializeConfigured => _reinitializeConfigured;

    internal MetadataBackupRestoreExecutor(
        string sevenZipExecutablePath,
        string dataDirectory,
        IDomainAdministrationStore domainStore,
        IAccountAdministrationStore accountStore,
        IAliasAdministrationStore aliasStore,
        IDistributionListAdministrationStore distributionListStore,
        IDistributionListRecipientAdministrationStore recipientStore,
        BackupRestoreDataDirectoryRuntime? dataDirectoryRuntime = null,
        Func<BackupRestoreDataDirectoryBoundary>? dataDirectoryBoundaryFactory = null,
        IBackupRestoreMetadataTransactionFactory? metadataTransactionFactory = null,
        bool requireSqlTransaction = false,
        IFetchAccountAdministrationStore? fetchAccountStore = null,
        IRuleAdministrationStore? ruleStore = null,
        IRuleCriteriaAdministrationStore? ruleCriteriaStore = null,
        IRuleActionAdministrationStore? ruleActionStore = null,
        IImapFolderAdministrationRestoreStore? folderRestoreStore = null,
        IImapFolderAdministrationRestoreDeletionStore? folderRestoreDeletionStore = null,
        IMessageAdministrationRestoreStore? messageRestoreStore = null,
        IMessageAdministrationStore? messageStore = null,
        Func<CancellationToken, ValueTask>? reinitialize = null,
        bool requireReinitialize = false,
        IGroupAdministrationStore? groupStore = null,
        IGroupMemberAdministrationStore? groupMemberStore = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sevenZipExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentNullException.ThrowIfNull(domainStore);
        ArgumentNullException.ThrowIfNull(accountStore);
        ArgumentNullException.ThrowIfNull(aliasStore);
        ArgumentNullException.ThrowIfNull(distributionListStore);
        ArgumentNullException.ThrowIfNull(recipientStore);

        _integrityRuntime = new BackupRestoreIntegrityRuntime(sevenZipExecutablePath);
        _metadataReader = new SevenZipBackupArchiveMetadataReader(sevenZipExecutablePath);
        _dataDirectory = dataDirectory;
        _domainStore = domainStore;
        _accountStore = accountStore;
        _aliasStore = aliasStore;
        _distributionListStore = distributionListStore;
        _recipientStore = recipientStore;
        _groupStore = groupStore;
        _groupMemberStore = groupMemberStore;
        _fetchAccountStore = fetchAccountStore;
        _ruleStore = ruleStore;
        _ruleCriteriaStore = ruleCriteriaStore;
        _ruleActionStore = ruleActionStore;
        _folderRestoreStore = folderRestoreStore;
        _folderRestoreDeletionStore = folderRestoreDeletionStore;
        _messageRestoreStore = messageRestoreStore;
        _messageStore = messageStore;
        _dataDirectoryRuntime = dataDirectoryRuntime ?? new BackupRestoreDataDirectoryRuntime(sevenZipExecutablePath);
        _dataDirectoryBoundaryFactory = dataDirectoryBoundaryFactory
            ?? (() => new BackupRestoreDataDirectoryBoundary(
                _dataDirectory,
                Path.Combine(Path.GetTempPath(), $"hmailserver-restore-{Guid.NewGuid():N}.rollback")));
        _metadataTransactionFactory = metadataTransactionFactory;
        _requireSqlTransaction = requireSqlTransaction;
        _reinitializeConfigured = reinitialize is not null || !requireReinitialize;
        _reinitialize = reinitialize ?? (static _ => ValueTask.CompletedTask);
    }

    public async ValueTask ExecuteAsync(Backup backup, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(backup);
        if (!_reinitializeConfigured)
        {
            throw new InvalidOperationException(
                "Restore reinitialization is not configured for the production runtime.");
        }
        if (backup.RestoreOptions == BackupStartPlan.BackupSettingsFlag)
        {
            if (!backup.ContainsSettings)
            {
                throw new InvalidOperationException(
                    "Only RestoreDomains, RestoreSettings, or RestoreMessages sections contained in the backup can be selected.");
            }

            await ExecuteSettingsOnlyRestoreAsync(backup, cancellationToken).ConfigureAwait(false);
            await _reinitialize(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (backup.RestoreOptions == SupportedDataRestoreOptions)
        {
            await ExecuteNonDbDataRestoreAsync(backup, fullRestore: false, cancellationToken)
                .ConfigureAwait(false);
            await _reinitialize(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (backup.RestoreOptions == SupportedFullRestoreOptions)
        {
            await ExecuteNonDbDataRestoreAsync(backup, fullRestore: true, cancellationToken)
                .ConfigureAwait(false);
            await _reinitialize(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (backup.RestoreOptions is not (SupportedDbOnlyRestoreOptions
            or SupportedDbOnlyRestoreOptionsWithSettings))
        {
            throw new InvalidOperationException(
                "Only RestoreDomains (DB-only), RestoreDomains|RestoreMessages (non-DB-only), or full RestoreSettings|RestoreDomains|RestoreMessages is supported.");
        }

        await ExecuteDbOnlyMetadataRestoreAsync(backup, cancellationToken).ConfigureAwait(false);
        await _reinitialize(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ExecuteSettingsOnlyRestoreAsync(
        Backup backup,
        CancellationToken cancellationToken)
    {
        using var archiveReadLock = BackupArchiveIdentity.OpenReadLock(backup.ArchivePath);
        EnsureArchiveIdentity(backup);
        var evidence = await _integrityRuntime
            .InspectAsync(backup.ArchivePath, cancellationToken, backupMessagesDbOnly: true)
            .ConfigureAwait(false);
        var dryRun = BackupRestoreDryRunPlanner.Plan(evidence, backup.RestoreOptions);
        if (!dryRun.EvidenceIsValid
            || dryRun.FailureReason is not null
            || dryRun.MissingRestoreOptions != 0
            || !dryRun.RestoreSettings
            || dryRun.RestoreDomains
            || dryRun.RestoreMessages
            || dryRun.RequiresFilesystemStaging
            || !dryRun.Steps.Contains(BackupRestoreDryRunPlanner.LoadSettingsStep))
        {
            throw new InvalidOperationException(
                dryRun.FailureReason ?? "Only settings-only DB restore is supported by this slice.");
        }

        EnsureArchiveIdentity(backup);
        var archiveXml = _metadataReader.ReadMetadataXml(backup.ArchivePath);
        EnsureArchiveIdentity(backup);
        var properties = BackupArchiveXmlSnapshotParser.ParseSettingsProperties(archiveXml);
        var securityRanges = BackupArchiveXmlSnapshotParser.ParseSecurityRanges(archiveXml);
        var tcpIpPorts = BackupArchiveXmlSnapshotParser.ParseTcpIpPorts(archiveXml);
        var archiveGroups = BackupArchiveXmlSnapshotParser.ParseGroupEntries(archiveXml);
        if (properties.Any(static property =>
                string.Equals(property.Name, "smtprelayerpassword", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                "Settings restore archives must not contain the SMTP relayer credential property.");
        }

        using var authorizationLease = await backup
            .AcquireAuthorizationLeaseAsync(cancellationToken)
            .ConfigureAwait(false);
        if (_metadataTransactionFactory is null || !_requireSqlTransaction)
        {
            throw new InvalidOperationException(
                "Settings-only restore requires a SQL metadata transaction factory.");
        }

        await using var metadataTransaction = await _metadataTransactionFactory
            .BeginAsync(cancellationToken)
            .ConfigureAwait(false);
        var settingsStore = metadataTransaction.SettingsStore
            ?? throw new InvalidOperationException(
                "Settings-only restore requires a transaction-scoped settings store.");
        var securityRangeStore = metadataTransaction.SecurityRangeStore
            ?? throw new InvalidOperationException(
                "Settings-only restore requires a transaction-scoped security-range store.");
        var tcpIpPortStore = metadataTransaction.TcpIpPortStore
            ?? throw new InvalidOperationException(
                "Settings-only restore requires a transaction-scoped TCP/IP port store.");
        await metadataTransaction.DeleteAllSecurityRangesForRestoreAsync(cancellationToken)
            .ConfigureAwait(false);
        await metadataTransaction.DeleteAllTcpIpPortsForRestoreAsync(cancellationToken)
            .ConfigureAwait(false);
        await metadataTransaction.DeleteAllGroupsForRestoreAsync(cancellationToken)
            .ConfigureAwait(false);
        if (archiveGroups.Count > 0)
        {
            var groupStore = metadataTransaction.GroupStore
                ?? throw new InvalidOperationException(
                    "Settings-only restore requires a transaction-scoped group store.");
            var groupMemberStore = metadataTransaction.GroupMemberStore
                ?? throw new InvalidOperationException(
                    "Settings-only restore requires a transaction-scoped group-member store.");
            var domains = await metadataTransaction.DomainStore
                .GetDomainsAsync(cancellationToken).ConfigureAwait(false);
            var accounts = new List<AccountAdministrationSnapshot>();
            foreach (var domain in domains)
            {
                accounts.AddRange(await metadataTransaction.AccountStore
                    .GetAccountsAsync(domain.Id, cancellationToken).ConfigureAwait(false));
            }
            await metadataTransaction.DeleteAllGroupsForRestoreAsync(cancellationToken).ConfigureAwait(false);
            await BackupRestoreMetadataWriter.RestoreGroupsAsync(
                archiveGroups,
                accounts,
                groupStore,
                groupMemberStore,
                static () => default,
                cancellationToken).ConfigureAwait(false);
        }
        await settingsStore
            .RestoreSettingsPropertiesAsync(properties, cancellationToken)
            .ConfigureAwait(false);
        await BackupRestoreMetadataWriter.RestoreSecurityRangesAsync(
            securityRanges,
            securityRangeStore,
            static () => default,
            cancellationToken).ConfigureAwait(false);
        await BackupRestoreMetadataWriter.RestoreTcpIpPortsAsync(
            tcpIpPorts,
            tcpIpPortStore,
            static () => default,
            cancellationToken).ConfigureAwait(false);
        await metadataTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ExecuteDbOnlyMetadataRestoreAsync(
        Backup backup,
        CancellationToken cancellationToken)
    {

        using var archiveReadLock = BackupArchiveIdentity.OpenReadLock(backup.ArchivePath);
        EnsureArchiveIdentity(backup);
        var evidence = await _integrityRuntime
            .InspectAsync(backup.ArchivePath, cancellationToken, backupMessagesDbOnly: true)
            .ConfigureAwait(false);
        var dryRun = BackupRestoreDryRunPlanner.Plan(evidence, backup.RestoreOptions);
        if (!dryRun.EvidenceIsValid
            || dryRun.FailureReason is not null
            || dryRun.MissingRestoreOptions != 0
            || !dryRun.RestoreDomains
            || dryRun.RestoreMessages
            || dryRun.RequiresFilesystemStaging
            || !dryRun.Steps.Contains(BackupRestoreDryRunPlanner.LoadDomainsAndChildrenStep)
            || dryRun.Steps.Contains(BackupRestoreDryRunPlanner.RestoreDataDirectoryStep))
        {
            throw new InvalidOperationException(
                dryRun.FailureReason ?? "The restore options are not supported by the DB-only metadata restore slice.");
        }

        if (evidence.BackupOptions != backup.RestoreOptions
            || evidence.BackupOptions is not (BackupStartPlan.BackupDomainsFlag
                or (BackupStartPlan.BackupSettingsFlag | BackupStartPlan.BackupDomainsFlag)))
        {
            throw new InvalidOperationException(
                "The archive contains restore sections outside the DB-only metadata restore slice.");
        }

        var rollbackArtifactPath = Path.Combine(
            Path.GetTempPath(),
            $"hmailserver-restore-{Guid.NewGuid():N}.rollback");
        var containment = BackupRestoreContainmentPreflight.Plan(
            evidence,
            _dataDirectory,
            rollbackArtifactPath,
            cancellationToken);
        if (!containment.IsSafe)
        {
            throw new InvalidOperationException(
                containment.FailureReason ?? "The restore containment preflight failed.");
        }

        var revalidatedContainment = await BackupRestoreContainmentPreflight
            .RevalidateAsync(
                containment,
                evidence,
                _integrityRuntime,
                cancellationToken)
            .ConfigureAwait(false);
        if (!revalidatedContainment.IsSafe)
        {
            throw new InvalidOperationException(
                revalidatedContainment.FailureReason
                    ?? "The restore containment revalidation failed.");
        }

        EnsureArchiveIdentity(backup);
        var archiveXml = _metadataReader.ReadMetadataXml(backup.ArchivePath);
        EnsureArchiveIdentity(backup);
        var domains = BackupArchiveXmlSnapshotParser.ParseDomainEntries(archiveXml);
        if (domains.Count == 0)
        {
            throw new InvalidDataException("The backup contains no domain metadata to restore.");
        }

        IReadOnlyList<BackupSettingsPropertySnapshot>? settingsProperties = null;
        IReadOnlyList<SecurityRangeAdministrationSnapshot>? securityRanges = null;
        IReadOnlyList<RestoreTcpIpPortEntry>? tcpIpPorts = null;
        IReadOnlyList<RestoreGroupEntry>? archiveGroups = null;
        if (backup.RestoreSettings)
        {
            archiveGroups = BackupArchiveXmlSnapshotParser.ParseGroupEntries(archiveXml);
        }
        if (backup.RestoreSettings)
        {
            settingsProperties = BackupArchiveXmlSnapshotParser.ParseSettingsProperties(archiveXml);
            securityRanges = BackupArchiveXmlSnapshotParser.ParseSecurityRanges(archiveXml);
            tcpIpPorts = BackupArchiveXmlSnapshotParser.ParseTcpIpPorts(archiveXml);
            if (settingsProperties.Any(static property =>
                    string.Equals(property.Name, "smtprelayerpassword", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException(
                    "Settings restore archives must not contain the SMTP relayer credential property.");
            }
        }

        await RestoreMetadataAsync(
            domains,
            requireEmptyStore: false,
            useSqlTransaction: true,
            authorizationLeaseFactory: backup.AcquireAuthorizationLeaseAsync,
            cancellationToken: cancellationToken,
            settingsProperties: settingsProperties,
            securityRanges: securityRanges,
            tcpIpPorts: tcpIpPorts,
            archiveGroups: archiveGroups).ConfigureAwait(false);
    }

    private async ValueTask ExecuteNonDbDataRestoreAsync(
        Backup backup,
        bool fullRestore,
        CancellationToken cancellationToken)
    {
        using var archiveReadLock = BackupArchiveIdentity.OpenReadLock(backup.ArchivePath);
        EnsureArchiveIdentity(backup);
        var evidence = await _integrityRuntime
            .InspectAsync(backup.ArchivePath, cancellationToken)
            .ConfigureAwait(false);
        var dryRun = BackupRestoreDryRunPlanner.Plan(evidence, backup.RestoreOptions);
        if (!dryRun.EvidenceIsValid
            || dryRun.FailureReason is not null
            || dryRun.MissingRestoreOptions != 0
            || (!fullRestore && dryRun.RestoreSettings)
            || !dryRun.RestoreDomains
            || !dryRun.RestoreMessages
            || !dryRun.RequiresFilesystemStaging
            || !dryRun.Steps.Contains(BackupRestoreDryRunPlanner.RestoreDataDirectoryStep))
        {
            throw new InvalidOperationException(
                dryRun.FailureReason
                    ?? (fullRestore
                        ? "Only full RestoreSettings|RestoreDomains|RestoreMessages non-DB-only restore is supported by this slice."
                        : "Only RestoreDomains|RestoreMessages non-DB-only restore is supported by this slice."));
        }

        var expectedRestoreOptions = fullRestore
            ? SupportedFullRestoreOptions
            : SupportedDataRestoreOptions;
        if (evidence.BackupOptions is not int backupOptions
            || (backupOptions & (BackupStartPlan.BackupSettingsFlag
                | BackupStartPlan.BackupDomainsFlag
                | BackupStartPlan.BackupMessagesFlag))
                != expectedRestoreOptions
            || evidence.BackupMessagesDbOnly)
        {
            throw new InvalidOperationException(
                fullRestore
                    ? "The archive is not a full non-DB-only RestoreSettings|RestoreDomains|RestoreMessages backup."
                    : "The archive is not a non-DB-only RestoreDomains|RestoreMessages backup.");
        }

        if (fullRestore && _metadataTransactionFactory is null)
        {
            throw new InvalidOperationException(
                "Full restore requires a SQL metadata transaction factory.");
        }

        using var boundary = _dataDirectoryBoundaryFactory();
        var containment = BackupRestoreContainmentPreflight.Plan(
            evidence,
            boundary.TargetDataDirectoryPath,
            boundary.RollbackArtifactPath,
            cancellationToken);
        if (!containment.IsSafe)
        {
            throw new InvalidOperationException(
                containment.FailureReason ?? "The restore containment preflight failed.");
        }

        var revalidatedContainment = await BackupRestoreContainmentPreflight
            .RevalidateAsync(
                containment,
                evidence,
                _integrityRuntime,
                cancellationToken)
            .ConfigureAwait(false);
        if (!revalidatedContainment.IsSafe)
        {
            throw new InvalidOperationException(
                revalidatedContainment.FailureReason
                    ?? "The restore containment revalidation failed.");
        }

        EnsureArchiveIdentity(backup);
        var archiveXml = _metadataReader.ReadMetadataXml(backup.ArchivePath);
        EnsureArchiveIdentity(backup);
        var domains = BackupArchiveXmlSnapshotParser.ParseDomainEntries(archiveXml);
        if (domains.Count == 0)
        {
            throw new InvalidDataException("The backup contains no domain metadata to restore.");
        }

        IReadOnlyList<BackupSettingsPropertySnapshot>? settingsProperties = null;
        IReadOnlyList<SecurityRangeAdministrationSnapshot>? securityRanges = null;
        IReadOnlyList<RestoreTcpIpPortEntry>? tcpIpPorts = null;
        IReadOnlyList<RestoreGroupEntry>? archiveGroups = null;
        IReadOnlyList<RestorePublicFolderEntry>? publicFolders = null;
        if (fullRestore)
        {
            publicFolders = BackupArchiveXmlSnapshotParser.ParsePublicFolderEntries(archiveXml);
            archiveGroups = BackupArchiveXmlSnapshotParser.ParseGroupEntries(archiveXml);
            settingsProperties = BackupArchiveXmlSnapshotParser.ParseSettingsProperties(archiveXml);
            securityRanges = BackupArchiveXmlSnapshotParser.ParseSecurityRanges(archiveXml);
            tcpIpPorts = BackupArchiveXmlSnapshotParser.ParseTcpIpPorts(archiveXml);
            if (settingsProperties.Any(static property =>
                    string.Equals(property.Name, "smtprelayerpassword", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException(
                    "Settings restore archives must not contain the SMTP relayer credential.");
            }
        }

        using var authorizationLease = await backup
            .AcquireAuthorizationLeaseAsync(cancellationToken)
            .ConfigureAwait(false);
        var finalContainment = BackupRestoreContainmentPreflight.Revalidate(
            revalidatedContainment,
            evidence,
            cancellationToken);
        if (!finalContainment.IsSafe)
        {
            throw new InvalidOperationException(
                finalContainment.FailureReason
                    ?? "The final restore containment revalidation failed.");
        }

        await _dataDirectoryRuntime
            .RestoreAsync(
                evidence,
                finalContainment,
                cancellationToken,
                commitAsync: ct => RestoreMetadataAsync(
                    domains,
                    requireEmptyStore: !fullRestore,
                    useSqlTransaction: fullRestore,
                    authorizationLeaseFactory: null,
                     cancellationToken: ct,
                     settingsProperties: settingsProperties,
                     securityRanges: securityRanges,
                     tcpIpPorts: tcpIpPorts,
                     restorePublicFolders: fullRestore,
                    publicFolders: publicFolders,
                    archiveGroups: archiveGroups),
                commitOutcomeMayBeAmbiguous: fullRestore)
            .ConfigureAwait(false);
    }

    private async ValueTask RestoreMetadataAsync(
        IReadOnlyList<RestoreDomainEntry> domains,
        bool requireEmptyStore,
        bool useSqlTransaction,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory,
        CancellationToken cancellationToken,
        IReadOnlyList<BackupSettingsPropertySnapshot>? settingsProperties = null,
        IReadOnlyList<SecurityRangeAdministrationSnapshot>? securityRanges = null,
        IReadOnlyList<RestoreTcpIpPortEntry>? tcpIpPorts = null,
        bool restorePublicFolders = false,
        IReadOnlyList<RestorePublicFolderEntry>? publicFolders = null,
        IReadOnlyList<RestoreGroupEntry>? archiveGroups = null)
    {
        var existingDomains = await _domainStore
            .GetDomainsAsync(cancellationToken)
            .ConfigureAwait(false);
        if (requireEmptyStore && existingDomains.Count != 0)
        {
            throw new InvalidOperationException(
                "Non-DB-only restore requires an empty disposable domain store.");
        }

        var existingDomainNames = domains
            .Select(static domain => domain.Domain.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (domains.Any(domain =>
                string.IsNullOrWhiteSpace(domain.Domain.Name)
                || !existingDomainNames.Remove(domain.Domain.Name)))
        {
            throw new InvalidOperationException("The restore contains a duplicate or empty domain name.");
        }

        var insertedDomainIds = new List<int>();
        var insertedAccountIds = new List<(int DomainId, int AccountId)>();
        var insertedFetchAccountIds = new List<(int AccountId, int FetchAccountId)>();
        var insertedAliasIds = new List<(int DomainId, int AliasId)>();
        var insertedDistributionListIds = new List<(int DomainId, int ListId)>();
        var insertedRecipientIds = new List<(int ListId, int RecipientId, string Address)>();
        var insertedRuleIds = new List<(int AccountId, int RuleId)>();
        var insertedFolderRootIds = new List<(int AccountId, int FolderId, int ParentId)>();
        var insertedMessageIds = new List<(int AccountId, int FolderId, long MessageId)>();
        var restoredAccounts = new List<AccountAdministrationSnapshot>();

        IBackupRestoreMetadataTransaction? metadataTransaction = null;
        using var authorizationLease = authorizationLeaseFactory is null
            ? null
            : await authorizationLeaseFactory(cancellationToken).ConfigureAwait(false);
        try
        {
            if (useSqlTransaction)
            {
                if (_metadataTransactionFactory is null && _requireSqlTransaction)
                {
                    throw new InvalidOperationException(
                        "DB-only restore requires a SQL metadata transaction factory.");
                }

                if (_metadataTransactionFactory is not null)
                {
                    metadataTransaction = await _metadataTransactionFactory
                        .BeginAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            var domainStore = metadataTransaction?.DomainStore ?? _domainStore;
            var accountStore = metadataTransaction?.AccountStore ?? _accountStore;
            var aliasStore = metadataTransaction?.AliasStore ?? _aliasStore;
            var distributionListStore = metadataTransaction?.DistributionListStore ?? _distributionListStore;
            var recipientStore = metadataTransaction?.RecipientStore ?? _recipientStore;
            var fetchAccountStore = metadataTransaction?.FetchAccountStore ?? _fetchAccountStore;
            var ruleStore = metadataTransaction?.RuleStore ?? _ruleStore;
            var ruleCriteriaStore = metadataTransaction?.RuleCriteriaStore ?? _ruleCriteriaStore;
            var ruleActionStore = metadataTransaction?.RuleActionStore ?? _ruleActionStore;
            var folderRestoreStore = metadataTransaction?.FolderRestoreStore ?? _folderRestoreStore;
            var messageRestoreStore = metadataTransaction?.MessageRestoreStore ?? _messageRestoreStore;
            var groupStore = metadataTransaction?.GroupStore ?? _groupStore;
            var groupMemberStore = metadataTransaction?.GroupMemberStore ?? _groupMemberStore;
            var settingsStore = metadataTransaction?.SettingsStore;
            var securityRangeStore = metadataTransaction?.SecurityRangeStore;
            var tcpIpPortStore = metadataTransaction?.TcpIpPortStore;
            if (settingsProperties is not null && settingsStore is null)
            {
                throw new InvalidOperationException(
                    "Settings restore requires a transaction-scoped settings store.");
            }
            if (securityRanges is not null && securityRangeStore is null)
            {
                throw new InvalidOperationException(
                    "Settings restore requires a transaction-scoped security-range store.");
            }
            if (tcpIpPorts is not null && tcpIpPortStore is null)
            {
                throw new InvalidOperationException(
                    "Settings restore requires a transaction-scoped TCP/IP port store.");
            }
            if (domains.SelectMany(static domain => domain.Accounts).Any(static account => account.Folders.Count > 0)
                && folderRestoreStore is null)
            {
                throw new InvalidOperationException("Folder restore requires a folder administration restore store.");
            }
            if (!useSqlTransaction
                && domains.SelectMany(static domain => domain.Accounts).Any(static account => account.Folders.Count > 0)
                && _folderRestoreDeletionStore is null)
            {
                throw new InvalidOperationException(
                    "Non-transaction folder restore requires a folder restore deletion store for rollback.");
            }
            if (domains.SelectMany(static domain => domain.Accounts)
                    .SelectMany(static account => account.Folders)
                    .Any(static folder => folder.Messages.Count > 0)
                && messageRestoreStore is null)
            {
                throw new InvalidOperationException("Message restore requires a message administration restore store.");
            }
            if (domains.SelectMany(static domain => domain.Accounts).Any(static account => account.Rules.Count > 0)
                && (ruleStore is null || ruleCriteriaStore is null || ruleActionStore is null))
            {
                throw new InvalidOperationException(
                    "Rule restore requires rule, rule-criteria, and rule-action administration stores.");
            }
            if (archiveGroups is { Count: > 0 } && (groupStore is null || groupMemberStore is null))
            {
                throw new InvalidOperationException(
                    "Group restore requires group and group-member administration stores.");
            }
            Func<CancellationToken, ValueTask> commitAsync = metadataTransaction is null
                ? static _ => default
                : metadataTransaction.CommitAsync;
            Func<ValueTask> rollbackAsync = metadataTransaction is null
                ? () => RollbackAsync(
                    insertedDomainIds,
                    insertedAccountIds,
                    insertedAliasIds,
                    insertedDistributionListIds,
                    insertedRecipientIds,
                    insertedFetchAccountIds,
                    insertedRuleIds,
                    insertedFolderRootIds,
                    insertedMessageIds)
                : static () => default;

            if (useSqlTransaction && metadataTransaction is not null)
            {
                await metadataTransaction
                    .DeleteAllDomainsForRestoreAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (archiveGroups is not null)
                {
                    await metadataTransaction
                        .DeleteAllGroupsForRestoreAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                if (securityRanges is not null)
                {
                    await metadataTransaction
                        .DeleteAllSecurityRangesForRestoreAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                if (tcpIpPorts is not null)
                {
                    await metadataTransaction
                        .DeleteAllTcpIpPortsForRestoreAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                if (restorePublicFolders)
                {
                    await metadataTransaction
                        .DeleteAllPublicFoldersForRestoreAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            await BackupRestoreTransactionBoundary.ExecuteAsync(
                mutateAsync: async ct =>
                {
                    await BackupRestoreMetadataWriter.RestoreDomainsAsync(
                        domains.Select(static entry => entry.Domain).ToArray(),
                        domainStore,
                        static () => default,
                        ct,
                        insertedDomainIds.Add).ConfigureAwait(false);

                    if (insertedDomainIds.Count != domains.Count)
                    {
                        throw new InvalidOperationException("The domain restore did not return all inserted IDs.");
                    }

                    for (var index = 0; index < domains.Count; index++)
                    {
                        var domainEntry = domains[index];
                        var domainId = insertedDomainIds[index];
                        var accounts = domainEntry.Accounts
                            .Select(account => account with
                            {
                                Account = account.Account with { DomainId = domainId }
                            })
                            .ToArray();
                        var aliases = domainEntry.Aliases
                            .Select(alias => alias with { DomainId = domainId })
                            .ToArray();

                        var insertedAccountStart = insertedAccountIds.Count;
                        var restoredAccountIndex = 0;
                        await BackupRestoreMetadataWriter.RestoreAccountsAsync(
                            accounts,
                            domainId,
                            accountStore,
                            static () => default,
                            ct,
                            accountId =>
                            {
                                insertedAccountIds.Add((domainId, accountId));
                                var sourceAccount = accounts[restoredAccountIndex++].Account;
                                restoredAccounts.Add(sourceAccount with { Id = accountId, DomainId = domainId });
                            }).ConfigureAwait(false);

                        if (domainEntry.Accounts.Any(static account => account.FetchAccounts.Count > 0)
                            && fetchAccountStore is null)
                        {
                            throw new InvalidOperationException(
                                "Fetch-account restore requires a fetch-account administration store.");
                        }

                        var accountIndex = 0;
                        foreach (var account in domainEntry.Accounts)
                        {
                            if (account.FetchAccounts.Count == 0)
                            {
                                accountIndex++;
                                continue;
                            }

                            var restoredAccountId = insertedAccountIds[insertedAccountStart + accountIndex].AccountId;
                            await BackupRestoreMetadataWriter.RestoreFetchAccountsAsync(
                                account.FetchAccounts,
                                restoredAccountId,
                                fetchAccountStore!,
                                static () => default,
                                ct,
                                fetchAccountId => insertedFetchAccountIds.Add((restoredAccountId, fetchAccountId))).ConfigureAwait(false);
                            accountIndex++;
                        }

                        if (domainEntry.Accounts.Any(static account => account.Rules.Count > 0))
                        {
                            accountIndex = 0;
                            foreach (var account in domainEntry.Accounts)
                            {
                                var restoredAccountId = insertedAccountIds[insertedAccountStart + accountIndex].AccountId;
                                await BackupRestoreMetadataWriter.RestoreRulesAsync(
                                    account.Rules,
                                    restoredAccountId,
                                    ruleStore!,
                                    ruleCriteriaStore!,
                                    ruleActionStore!,
                                    static () => default,
                                    ct,
                                    ruleId => insertedRuleIds.Add((restoredAccountId, ruleId))).ConfigureAwait(false);
                                accountIndex++;
                            }
                        }

                        if (domainEntry.Accounts.Any(static account => account.Folders.Count > 0))
                        {
                            accountIndex = 0;
                            foreach (var account in domainEntry.Accounts)
                            {
                                var restoredAccountId = insertedAccountIds[insertedAccountStart + accountIndex].AccountId;
                                await BackupRestoreMetadataWriter.RestoreFoldersAsync(
                                    account.Folders,
                                    restoredAccountId,
                                    folderRestoreStore!,
                                    messageRestoreStore!,
                                    static () => default,
                                    ct,
                                    folderId => insertedFolderRootIds.Add(
                                        (restoredAccountId, folderId, -1)),
                                    (folderId, messageId) => insertedMessageIds.Add((restoredAccountId, folderId, messageId))).ConfigureAwait(false);
                                accountIndex++;
                            }
                        }

                        await BackupRestoreMetadataWriter.RestoreAliasesAsync(
                            aliases,
                            domainId,
                            aliasStore,
                            static () => default,
                            ct,
                            aliasId => insertedAliasIds.Add((domainId, aliasId))).ConfigureAwait(false);

                        foreach (var listEntry in domainEntry.DistributionLists)
                        {
                            var distributionList = listEntry.DistributionList with { DomainId = domainId };
                            await BackupRestoreMetadataWriter.RestoreDistributionListsAsync(
                                new[] { distributionList },
                                domainId,
                                distributionListStore,
                                static () => default,
                                ct,
                                listId => insertedDistributionListIds.Add((domainId, listId))).ConfigureAwait(false);

                            if (insertedDistributionListIds.Count == 0)
                            {
                                throw new InvalidOperationException("The distribution-list restore did not return an inserted ID.");
                            }

                            var listId = insertedDistributionListIds[^1].ListId;
                            var recipientIndex = 0;
                            await BackupRestoreMetadataWriter.RestoreDistributionListRecipientsAsync(
                                listEntry.Recipients,
                                listId,
                                recipientStore,
                                static () => default,
                                ct,
                                recipientId => insertedRecipientIds.Add(
                                    (listId, recipientId, listEntry.Recipients[recipientIndex++].Address))).ConfigureAwait(false);
                        }
                    }

                    var restoredGroups = archiveGroups is { Count: > 0 }
                        ? await BackupRestoreMetadataWriter.RestoreGroupsAsync(
                            archiveGroups,
                            restoredAccounts,
                            groupStore!,
                            groupMemberStore!,
                            static () => default,
                            ct).ConfigureAwait(false)
                        : Array.Empty<GroupAdministrationSnapshot>();

                    if (restorePublicFolders && publicFolders is { Count: > 0 })
                    {
                        var permissionStore = metadataTransaction?.FolderPermissionRestoreStore;
                        if (folderRestoreStore is null
                            || messageRestoreStore is null
                            || permissionStore is null)
                        {
                            throw new InvalidOperationException(
                                "Public-folder restore requires transaction-scoped folder, message, and permission stores.");
                        }

                        var groups = restoredGroups.Count > 0
                            ? restoredGroups
                            : _groupStore is null
                                ? Array.Empty<GroupAdministrationSnapshot>()
                                : await _groupStore.GetGroupsAsync(ct).ConfigureAwait(false);
                        await BackupRestoreMetadataWriter.RestorePublicFoldersAsync(
                            publicFolders,
                            restoredAccounts,
                            groups,
                            folderRestoreStore,
                            messageRestoreStore,
                            permissionStore,
                            static () => default,
                            ct).ConfigureAwait(false);
                    }

                    if (settingsProperties is not null)
                    {
                        await settingsStore!
                            .RestoreSettingsPropertiesAsync(settingsProperties, ct)
                            .ConfigureAwait(false);
                    }
                    if (securityRanges is not null)
                    {
                        await BackupRestoreMetadataWriter.RestoreSecurityRangesAsync(
                            securityRanges,
                            securityRangeStore!,
                            static () => default,
                            ct).ConfigureAwait(false);
                    }
                    if (tcpIpPorts is not null)
                    {
                        await BackupRestoreMetadataWriter.RestoreTcpIpPortsAsync(
                            tcpIpPorts,
                            tcpIpPortStore!,
                            static () => default,
                            ct).ConfigureAwait(false);
                    }
                },
                commitAsync,
                rollbackAsync,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (metadataTransaction is not null)
            {
                await metadataTransaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async ValueTask RollbackAsync(
        IReadOnlyList<int> domainIds,
        IReadOnlyList<(int DomainId, int AccountId)> accountIds,
        IReadOnlyList<(int DomainId, int AliasId)> aliasIds,
        IReadOnlyList<(int DomainId, int ListId)> distributionListIds,
        IReadOnlyList<(int ListId, int RecipientId, string Address)> recipientIds,
        IReadOnlyList<(int AccountId, int FetchAccountId)> fetchAccountIds,
        IReadOnlyList<(int AccountId, int RuleId)> ruleIds,
        IReadOnlyList<(int AccountId, int FolderId, int ParentId)> folderRootIds,
        IReadOnlyList<(int AccountId, int FolderId, long MessageId)> messageIds)
    {
        if (_messageStore is not null)
        {
            foreach (var item in messageIds.Reverse())
            {
                var deleted = await _messageStore.DeleteMessageAsync(
                    item.AccountId, item.FolderId, item.MessageId, CancellationToken.None).ConfigureAwait(false);
                if (!deleted)
                {
                    throw new InvalidOperationException("Restore rollback could not delete a message.");
                }
            }
        }
        if (_folderRestoreDeletionStore is not null)
        {
            foreach (var item in folderRootIds.Reverse())
            {
                var deleted = await _folderRestoreDeletionStore.DeleteRestoredFolderTreeAsync(
                    item.AccountId,
                    item.FolderId,
                    item.ParentId,
                    CancellationToken.None).ConfigureAwait(false);
                if (!deleted)
                {
                    throw new InvalidOperationException("Restore rollback could not delete a folder tree.");
                }
            }
        }

        await RollbackFetchAccountsAsync(fetchAccountIds).ConfigureAwait(false);

        if (_ruleStore is not null)
        {
            foreach (var item in ruleIds.Reverse())
            {
                var deleted = await _ruleStore.DeleteRuleAsync(
                    item.AccountId,
                    item.RuleId,
                    CancellationToken.None).ConfigureAwait(false);
                if (!deleted)
                {
                    throw new InvalidOperationException("Restore rollback could not delete a rule.");
                }
            }
        }

        foreach (var item in recipientIds.Reverse())
        {
            var deleted = await _recipientStore.DeleteDistributionListRecipientAsync(
                    new DistributionListRecipientAdministrationSnapshot(item.RecipientId, item.ListId, item.Address),
                    CancellationToken.None).ConfigureAwait(false);
            if (!deleted)
            {
                throw new InvalidOperationException("Restore rollback could not delete a distribution-list recipient.");
            }
        }

        foreach (var item in distributionListIds.Reverse())
        {
            var deleted = await _distributionListStore.DeleteDistributionListAsync(
                    item.DomainId,
                    item.ListId,
                    CancellationToken.None).ConfigureAwait(false);
            if (!deleted)
            {
                throw new InvalidOperationException("Restore rollback could not delete a distribution list.");
            }
        }

        foreach (var item in aliasIds.Reverse())
        {
            var deleted = await _aliasStore.DeleteAliasAsync(
                    item.DomainId,
                    item.AliasId,
                    CancellationToken.None).ConfigureAwait(false);
            if (!deleted)
            {
                throw new InvalidOperationException("Restore rollback could not delete an alias.");
            }
        }

        foreach (var item in accountIds.Reverse())
        {
            var deleted = await _accountStore.DeleteAccountAsync(
                    item.DomainId,
                    item.AccountId,
                    CancellationToken.None).ConfigureAwait(false);
            if (!deleted)
            {
                throw new InvalidOperationException("Restore rollback could not delete an account.");
            }
        }

        foreach (var domainId in domainIds.Reverse())
        {
            var deleted = await _domainStore.DeleteDomainByIdAsync(domainId, CancellationToken.None).ConfigureAwait(false);
            if (!deleted)
            {
                throw new InvalidOperationException("Restore rollback could not delete a domain.");
            }
        }
    }

    private async ValueTask RollbackFetchAccountsAsync(
        IReadOnlyList<(int AccountId, int FetchAccountId)> fetchAccountIds)
    {
        if (_fetchAccountStore is not null)
        {
            foreach (var item in fetchAccountIds.Reverse())
            {
                await _fetchAccountStore.DeleteFetchAccountAsync(
                    item.AccountId,
                    item.FetchAccountId,
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private static void EnsureArchiveIdentity(Backup backup)
    {
        if (backup.ArchiveIdentity is not null
            && !backup.ArchiveIdentity.Matches(backup.ArchivePath))
        {
            throw new InvalidDataException(
                "The restore archive changed after it was loaded.");
        }

        if (backup.RawDataBackupIdentity is not null)
        {
            var rawDataBackupPath = Path.Combine(
                Path.GetDirectoryName(backup.ArchivePath)!,
                "DataBackup");
            if (!backup.RawDataBackupIdentity.Matches(rawDataBackupPath))
            {
                throw new InvalidDataException(
                    "The bound raw DataBackup snapshot changed after it was loaded.");
            }
        }
    }
}
