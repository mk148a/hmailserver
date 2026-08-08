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

    ValueTask CommitAsync(CancellationToken cancellationToken);
}
