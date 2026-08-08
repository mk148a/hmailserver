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
    private const int SupportedRestoreOptions = BackupStartPlan.BackupDomainsFlag;

    private readonly BackupRestoreIntegrityRuntime _integrityRuntime;
    private readonly string _dataDirectory;
    private readonly IDomainAdministrationStore _domainStore;
    private readonly IAccountAdministrationStore _accountStore;
    private readonly IAliasAdministrationStore _aliasStore;
    private readonly IDistributionListAdministrationStore _distributionListStore;
    private readonly IDistributionListRecipientAdministrationStore _recipientStore;
    private readonly SevenZipBackupArchiveMetadataReader _metadataReader;

    internal MetadataBackupRestoreExecutor(
        string sevenZipExecutablePath,
        string dataDirectory,
        IDomainAdministrationStore domainStore,
        IAccountAdministrationStore accountStore,
        IAliasAdministrationStore aliasStore,
        IDistributionListAdministrationStore distributionListStore,
        IDistributionListRecipientAdministrationStore recipientStore)
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
    }

    public async ValueTask ExecuteAsync(Backup backup, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(backup);
        if (backup.RestoreOptions != SupportedRestoreOptions)
        {
            throw new InvalidOperationException(
                "Only RestoreDomains is supported by the DB-only metadata restore slice.");
        }

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

        if (evidence.BackupOptions != SupportedRestoreOptions)
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

        var archiveXml = _metadataReader.ReadMetadataXml(backup.ArchivePath);
        var domains = BackupArchiveXmlSnapshotParser.ParseDomainEntries(archiveXml);
        if (domains.Count == 0)
        {
            throw new InvalidDataException("The backup contains no domain metadata to restore.");
        }

        var existingDomains = await _domainStore
            .GetDomainsAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingDomainNames = existingDomains
            .Select(static domain => domain.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (domains.Any(domain =>
                string.IsNullOrWhiteSpace(domain.Domain.Name)
                || !existingDomainNames.Add(domain.Domain.Name)))
        {
            throw new InvalidOperationException(
                "The restore would overwrite an existing or duplicate domain.");
        }

        var insertedDomainIds = new List<int>();
        var insertedAccountIds = new List<(int DomainId, int AccountId)>();
        var insertedAliasIds = new List<(int DomainId, int AliasId)>();
        var insertedDistributionListIds = new List<(int DomainId, int ListId)>();
        var insertedRecipientIds = new List<(int ListId, int RecipientId, string Address)>();

        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: async ct =>
            {
                await BackupRestoreMetadataWriter.RestoreDomainsAsync(
                    domains.Select(static entry => entry.Domain).ToArray(),
                    _domainStore,
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

                    await BackupRestoreMetadataWriter.RestoreAccountsAsync(
                        accounts,
                        domainId,
                        _accountStore,
                        static () => default,
                        ct,
                        accountId => insertedAccountIds.Add((domainId, accountId))).ConfigureAwait(false);
                    await BackupRestoreMetadataWriter.RestoreAliasesAsync(
                        aliases,
                        domainId,
                        _aliasStore,
                        static () => default,
                        ct,
                        aliasId => insertedAliasIds.Add((domainId, aliasId))).ConfigureAwait(false);

                    foreach (var listEntry in domainEntry.DistributionLists)
                    {
                        var distributionList = listEntry.DistributionList with { DomainId = domainId };
                        await BackupRestoreMetadataWriter.RestoreDistributionListsAsync(
                            new[] { distributionList },
                            domainId,
                            _distributionListStore,
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
                            _recipientStore,
                            static () => default,
                            ct,
                            recipientId => insertedRecipientIds.Add(
                                (listId, recipientId, listEntry.Recipients[recipientIndex++].Address))).ConfigureAwait(false);
                    }
                }
            },
            commitAsync: static _ => default,
            rollbackAsync: () => RollbackAsync(
                insertedDomainIds,
                insertedAccountIds,
                insertedAliasIds,
                insertedDistributionListIds,
                insertedRecipientIds),
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask RollbackAsync(
        IReadOnlyList<int> domainIds,
        IReadOnlyList<(int DomainId, int AccountId)> accountIds,
        IReadOnlyList<(int DomainId, int AliasId)> aliasIds,
        IReadOnlyList<(int DomainId, int ListId)> distributionListIds,
        IReadOnlyList<(int ListId, int RecipientId, string Address)> recipientIds)
    {
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
}
