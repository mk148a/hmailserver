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
    private const int SupportedDataRestoreOptions =
        BackupStartPlan.BackupDomainsFlag | BackupStartPlan.BackupMessagesFlag;

    private readonly BackupRestoreIntegrityRuntime _integrityRuntime;
    private readonly string _dataDirectory;
    private readonly IDomainAdministrationStore _domainStore;
    private readonly IAccountAdministrationStore _accountStore;
    private readonly IAliasAdministrationStore _aliasStore;
    private readonly IDistributionListAdministrationStore _distributionListStore;
    private readonly IDistributionListRecipientAdministrationStore _recipientStore;
    private readonly IFetchAccountAdministrationStore? _fetchAccountStore;
    private readonly SevenZipBackupArchiveMetadataReader _metadataReader;
    private readonly BackupRestoreDataDirectoryRuntime _dataDirectoryRuntime;
    private readonly Func<BackupRestoreDataDirectoryBoundary> _dataDirectoryBoundaryFactory;
    private readonly IBackupRestoreMetadataTransactionFactory? _metadataTransactionFactory;
    private readonly bool _requireSqlTransaction;

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
        IFetchAccountAdministrationStore? fetchAccountStore = null)
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
        _fetchAccountStore = fetchAccountStore;
        _dataDirectoryRuntime = dataDirectoryRuntime ?? new BackupRestoreDataDirectoryRuntime(sevenZipExecutablePath);
        _dataDirectoryBoundaryFactory = dataDirectoryBoundaryFactory
            ?? (() => new BackupRestoreDataDirectoryBoundary(
                _dataDirectory,
                Path.Combine(Path.GetTempPath(), $"hmailserver-restore-{Guid.NewGuid():N}.rollback")));
        _metadataTransactionFactory = metadataTransactionFactory;
        _requireSqlTransaction = requireSqlTransaction;
    }

    public async ValueTask ExecuteAsync(Backup backup, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(backup);
        if (backup.RestoreOptions == SupportedDataRestoreOptions)
        {
            await ExecuteNonDbDataRestoreAsync(backup, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (backup.RestoreOptions != SupportedDbOnlyRestoreOptions)
        {
            throw new InvalidOperationException(
                "Only RestoreDomains (DB-only) or RestoreDomains|RestoreMessages (non-DB-only) is supported.");
        }

        await ExecuteDbOnlyMetadataRestoreAsync(backup, cancellationToken).ConfigureAwait(false);
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
            || dryRun.RestoreSettings
            || dryRun.RestoreMessages
            || dryRun.RequiresFilesystemStaging
            || dryRun.Steps.Contains(BackupRestoreDryRunPlanner.LoadSettingsStep)
            || dryRun.Steps.Contains(BackupRestoreDryRunPlanner.RestoreDataDirectoryStep))
        {
            throw new InvalidOperationException(
                dryRun.FailureReason ?? "The restore options are not supported by the DB-only metadata restore slice.");
        }

        if (evidence.BackupOptions != SupportedDbOnlyRestoreOptions)
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

        await RestoreMetadataAsync(
            domains,
            requireEmptyStore: false,
            useSqlTransaction: true,
            authorizationLeaseFactory: backup.AcquireAuthorizationLeaseAsync,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ExecuteNonDbDataRestoreAsync(
        Backup backup,
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
            || dryRun.RestoreSettings
            || !dryRun.RestoreDomains
            || !dryRun.RestoreMessages
            || !dryRun.RequiresFilesystemStaging
            || !dryRun.Steps.Contains(BackupRestoreDryRunPlanner.RestoreDataDirectoryStep))
        {
            throw new InvalidOperationException(
                dryRun.FailureReason
                    ?? "Only RestoreDomains|RestoreMessages non-DB-only restore is supported by this slice.");
        }

        if (evidence.BackupOptions is not int backupOptions
            || (backupOptions & (BackupStartPlan.BackupSettingsFlag
                | BackupStartPlan.BackupDomainsFlag
                | BackupStartPlan.BackupMessagesFlag))
                != SupportedDataRestoreOptions
            || evidence.BackupMessagesDbOnly)
        {
            throw new InvalidOperationException(
                "The archive is not a non-DB-only RestoreDomains|RestoreMessages backup.");
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
                    requireEmptyStore: true,
                    useSqlTransaction: false,
                    authorizationLeaseFactory: null,
                    cancellationToken: ct),
                commitOutcomeMayBeAmbiguous: false)
            .ConfigureAwait(false);
    }

    private async ValueTask RestoreMetadataAsync(
        IReadOnlyList<RestoreDomainEntry> domains,
        bool requireEmptyStore,
        bool useSqlTransaction,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory,
        CancellationToken cancellationToken)
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
                    insertedFetchAccountIds)
                : static () => default;

            if (useSqlTransaction && metadataTransaction is not null)
            {
                await metadataTransaction
                    .DeleteAllDomainsForRestoreAsync(cancellationToken)
                    .ConfigureAwait(false);
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
                        await BackupRestoreMetadataWriter.RestoreAccountsAsync(
                            accounts,
                            domainId,
                            accountStore,
                            static () => default,
                            ct,
                            accountId => insertedAccountIds.Add((domainId, accountId))).ConfigureAwait(false);

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
        IReadOnlyList<(int AccountId, int FetchAccountId)> fetchAccountIds)
    {
        await RollbackFetchAccountsAsync(fetchAccountIds).ConfigureAwait(false);

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
