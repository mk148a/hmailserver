using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerBackupDomainProjectionSnapshotFactory
    : IBackupDomainProjectionSnapshotFactory
{
    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerBackupDomainProjectionSnapshotFactory(
        SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IBackupDomainProjectionSnapshot> BeginAsync(
        CancellationToken cancellationToken)
    {
        var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = (SqlTransaction)await connection
                .BeginTransactionAsync(IsolationLevel.Snapshot, cancellationToken)
                .ConfigureAwait(false);
            return new SqlServerBackupDomainProjectionSnapshot(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}

internal sealed class SqlServerBackupDomainProjectionSnapshot
    : IBackupDomainProjectionSnapshot
{
    private readonly SqlConnection _connection;
    private readonly SqlTransaction _transaction;

    internal SqlServerBackupDomainProjectionSnapshot(
        SqlConnection connection,
        SqlTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
        var context = new SqlServerBackupRestoreTransactionContext(connection, transaction);
        var settingsStore = new SqlServerSettingsAdministrationStore(context);
        SettingsStore = settingsStore;
        BackupSettingsPropertyStore = settingsStore;
        SecurityRangeStore = new SqlServerSecurityRangeAdministrationStore(context);
        TcpIpPortStore = new SqlServerTcpIpPortAdministrationStore(context);
        BlockedAttachmentStore = new SqlServerBlockedAttachmentAdministrationStore(context);
        SurblServerStore = new SqlServerSurblServerAdministrationStore(context);
        DnsBlackListStore = new SqlServerDnsBlackListAdministrationStore(context);
        GroupStore = new SqlServerGroupAdministrationStore(context);
        GroupMemberStore = new SqlServerGroupMemberAdministrationStore(context);
        DomainStore = new SqlServerDomainAdministrationStore(context);
        var accountStore = new SqlServerAccountAdministrationStore(context);
        AccountStore = accountStore;
        BackupAccountStore = accountStore;
        BackupFetchAccountStore = new SqlServerBackupFetchAccountAdministrationStore(context);
        var ruleStore = new SqlServerRuleAdministrationStore(context);
        BackupRuleStore = ruleStore;
        RuleCriteriaStore = new SqlServerRuleCriteriaAdministrationStore(context);
        RuleActionStore = new SqlServerRuleActionAdministrationStore(context);
        FolderStore = new SqlServerImapFolderAdministrationStore(context);
        MessageBackupStore = new SqlServerMessageAdministrationStore(context);
        DomainAliasStore = new SqlServerDomainAliasAdministrationStore(context);
        AliasStore = new SqlServerAliasAdministrationStore(context);
        DistributionListStore = new SqlServerDistributionListAdministrationStore(context);
        RecipientStore = new SqlServerDistributionListRecipientAdministrationStore(context);
    }

    public ISettingsAdministrationStore SettingsStore { get; }

    public IBackupSettingsPropertyStore BackupSettingsPropertyStore { get; }

    public ISecurityRangeAdministrationStore SecurityRangeStore { get; }

    public ITcpIpPortAdministrationStore TcpIpPortStore { get; }

    public IBlockedAttachmentAdministrationStore BlockedAttachmentStore { get; }

    public ISurblServerAdministrationStore SurblServerStore { get; }

    public IDnsBlackListAdministrationStore DnsBlackListStore { get; }

    public IGroupAdministrationStore GroupStore { get; }

    public IGroupMemberAdministrationStore GroupMemberStore { get; }

    public IDomainAdministrationStore DomainStore { get; }

    public IAccountAdministrationStore AccountStore { get; }

    public IBackupAccountAdministrationStore BackupAccountStore { get; }

    public IBackupFetchAccountAdministrationStore BackupFetchAccountStore { get; }

    public IBackupRuleAdministrationStore BackupRuleStore { get; }

    public IRuleCriteriaAdministrationStore RuleCriteriaStore { get; }

    public IRuleActionAdministrationStore RuleActionStore { get; }

    public IImapFolderAdministrationStore FolderStore { get; }

    public IMessageAdministrationBackupStore MessageBackupStore { get; }

    public IDomainAliasAdministrationStore DomainAliasStore { get; }

    public IAliasAdministrationStore AliasStore { get; }

    public IDistributionListAdministrationStore DistributionListStore { get; }

    public IDistributionListRecipientAdministrationStore RecipientStore { get; }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _transaction.RollbackAsync().ConfigureAwait(false);
        }
        finally
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
