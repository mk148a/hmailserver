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

    ValueTask DeleteAllDomainsForRestoreAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Transaction-scoped domain deletion for restore is not implemented by this transaction.");

    ValueTask DeleteAllPublicFoldersForRestoreAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Transaction-scoped public-folder deletion for restore is not implemented by this transaction.");

    ValueTask<IReadOnlyList<ImapFolderAdministrationDeletedMessage>>
        DeleteAllPublicFoldersForRestoreWithManifestAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Transaction-scoped public-folder deletion with a restore manifest is not implemented by this transaction.");

    ValueTask CommitAsync(CancellationToken cancellationToken);
}
