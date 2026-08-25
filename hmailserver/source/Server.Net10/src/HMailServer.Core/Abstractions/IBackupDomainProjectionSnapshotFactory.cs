namespace HMailServer.Core.Abstractions;

public interface IBackupDomainProjectionSnapshotFactory
{
    ValueTask<IBackupDomainProjectionSnapshot> BeginAsync(
        CancellationToken cancellationToken);
}

public interface IBackupDomainProjectionSnapshot : IAsyncDisposable
{
    IDomainAdministrationStore DomainStore { get; }

    IAccountAdministrationStore AccountStore { get; }

    IBackupAccountAdministrationStore BackupAccountStore { get; }

    IBackupFetchAccountAdministrationStore BackupFetchAccountStore { get; }

    IDomainAliasAdministrationStore DomainAliasStore { get; }

    IAliasAdministrationStore AliasStore { get; }

    IDistributionListAdministrationStore DistributionListStore { get; }

    IDistributionListRecipientAdministrationStore RecipientStore { get; }
}
