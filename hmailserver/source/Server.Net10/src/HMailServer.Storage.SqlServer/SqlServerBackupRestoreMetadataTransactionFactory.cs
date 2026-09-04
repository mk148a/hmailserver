using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerBackupRestoreMetadataTransactionFactory
    : IBackupRestoreMetadataTransactionFactory
{
    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerBackupRestoreMetadataTransactionFactory(
        SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IBackupRestoreMetadataTransaction> BeginAsync(
        CancellationToken cancellationToken)
    {
        var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = (SqlTransaction)await connection
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            return new SqlServerBackupRestoreMetadataTransaction(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}

internal sealed class SqlServerBackupRestoreMetadataTransaction
    : IBackupRestoreMetadataTransaction
{
    private readonly SqlConnection _connection;
    private readonly SqlTransaction _transaction;
    private readonly SqlServerDomainAdministrationStore _domainStore;
    private readonly SqlServerImapFolderAdministrationStore _publicFolderStore;
    private readonly SqlServerGroupAdministrationStore _groupStore;
    private readonly SqlServerSecurityRangeAdministrationStore _securityRangeStore;
    private readonly SqlServerTcpIpPortAdministrationStore _tcpIpPortStore;
    private readonly SqlServerBlockedAttachmentAdministrationStore _blockedAttachmentStore;
    private bool _commitStarted;
    private bool _committed;

    internal SqlServerBackupRestoreMetadataTransaction(
        SqlConnection connection,
        SqlTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
        var context = new SqlServerBackupRestoreTransactionContext(connection, transaction);
        _domainStore = new SqlServerDomainAdministrationStore(context);
        _publicFolderStore = new SqlServerImapFolderAdministrationStore(context);
        _groupStore = new SqlServerGroupAdministrationStore(context);
        _securityRangeStore = new SqlServerSecurityRangeAdministrationStore(context);
        _tcpIpPortStore = new SqlServerTcpIpPortAdministrationStore(context);
        _blockedAttachmentStore = new SqlServerBlockedAttachmentAdministrationStore(context);
        GroupStore = _groupStore;
        GroupMemberStore = new SqlServerGroupMemberAdministrationStore(context);
        SecurityRangeStore = _securityRangeStore;
        TcpIpPortStore = _tcpIpPortStore;
        BlockedAttachmentStore = _blockedAttachmentStore;
        DomainStore = _domainStore;
        AccountStore = new SqlServerAccountAdministrationStore(context);
        AliasStore = new SqlServerAliasAdministrationStore(context);
        DistributionListStore = new SqlServerDistributionListAdministrationStore(context);
        RecipientStore = new SqlServerDistributionListRecipientAdministrationStore(context);
        SettingsStore = new SqlServerSettingsRestoreAdministrationStore(context);
        FetchAccountStore = new SqlServerFetchAccountAdministrationStore(context);
        RuleStore = new SqlServerRuleAdministrationStore(context);
        RuleCriteriaStore = new SqlServerRuleCriteriaAdministrationStore(context);
        RuleActionStore = new SqlServerRuleActionAdministrationStore(context);
        FolderRestoreStore = _publicFolderStore;
        FolderPermissionRestoreStore = _publicFolderStore;
        MessageRestoreStore = new SqlServerMessageAdministrationStore(context);
    }

    public IDomainAdministrationStore DomainStore { get; }

    public IAccountAdministrationStore AccountStore { get; }

    public IAliasAdministrationStore AliasStore { get; }

    public IDistributionListAdministrationStore DistributionListStore { get; }

    public IDistributionListRecipientAdministrationStore RecipientStore { get; }

    public ISettingsRestoreAdministrationStore SettingsStore { get; }

    public IFetchAccountAdministrationStore FetchAccountStore { get; }

    public IRuleAdministrationStore RuleStore { get; }

    public IRuleCriteriaAdministrationStore RuleCriteriaStore { get; }

    public IRuleActionAdministrationStore RuleActionStore { get; }

    public IImapFolderAdministrationRestoreStore FolderRestoreStore { get; }

    public IImapFolderPermissionAdministrationRestoreStore FolderPermissionRestoreStore { get; }

    public IMessageAdministrationRestoreStore MessageRestoreStore { get; }

    public IGroupAdministrationStore GroupStore { get; }

    public IGroupMemberAdministrationStore GroupMemberStore { get; }

    public ISecurityRangeAdministrationStore SecurityRangeStore { get; }

    public ITcpIpPortAdministrationStore TcpIpPortStore { get; }

    public IBlockedAttachmentAdministrationStore BlockedAttachmentStore { get; }

    public ValueTask DeleteAllDomainsForRestoreAsync(CancellationToken cancellationToken) =>
        _domainStore.DeleteAllDomainsForRestoreAsync(cancellationToken);

    public ValueTask DeleteAllPublicFoldersForRestoreAsync(CancellationToken cancellationToken) =>
        _publicFolderStore.DeleteAllPublicFoldersForRestoreAsync(cancellationToken);

    public ValueTask DeleteAllGroupsForRestoreAsync(CancellationToken cancellationToken) =>
        _groupStore.DeleteAllGroupsForRestoreAsync(cancellationToken);

    public ValueTask DeleteAllSecurityRangesForRestoreAsync(CancellationToken cancellationToken) =>
        _securityRangeStore.DeleteAllSecurityRangesForRestoreAsync(cancellationToken);

    public ValueTask DeleteAllTcpIpPortsForRestoreAsync(CancellationToken cancellationToken) =>
        _tcpIpPortStore.DeleteAllTcpIpPortsForRestoreAsync(cancellationToken);

    public ValueTask DeleteAllBlockedAttachmentsForRestoreAsync(CancellationToken cancellationToken) =>
        _blockedAttachmentStore.DeleteAllBlockedAttachmentsForRestoreAsync(cancellationToken);

    public ValueTask<IReadOnlyList<ImapFolderAdministrationDeletedMessage>>
        DeleteAllPublicFoldersForRestoreWithManifestAsync(CancellationToken cancellationToken) =>
        _publicFolderStore.DeleteAllPublicFoldersForRestoreWithManifestAsync(cancellationToken);

    public async ValueTask CommitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _commitStarted = true;
        await _transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_committed)
            {
                try
                {
                    await _transaction.RollbackAsync().ConfigureAwait(false);
                }
                catch when (_commitStarted)
                {
                    // A failed commit may have already closed the transaction.
                    // Preserve the original commit failure while still attempting rollback.
                }
            }
        }
        finally
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}

internal sealed class SqlServerBackupRestoreTransactionContext
{
    internal SqlServerBackupRestoreTransactionContext(
        SqlConnection connection,
        SqlTransaction transaction)
    {
        Connection = connection;
        Transaction = transaction;
    }

    internal SqlConnection Connection { get; }

    internal SqlTransaction Transaction { get; }
}

internal sealed class SqlServerCommandLease : IAsyncDisposable
{
    private readonly SqlConnection? _connection;

    private SqlServerCommandLease(SqlCommand command, SqlConnection? connection)
    {
        Command = command;
        _connection = connection;
    }

    internal SqlCommand Command { get; }

    internal static async ValueTask<SqlServerCommandLease> OpenAsync(
        SqlServerConnectionFactory? connectionFactory,
        SqlServerBackupRestoreTransactionContext? transactionContext,
        string sql,
        CancellationToken cancellationToken)
    {
        if (transactionContext is not null)
        {
            return new SqlServerCommandLease(
                new SqlCommand(sql, transactionContext.Connection, transactionContext.Transaction),
                connection: null);
        }

        ArgumentNullException.ThrowIfNull(connectionFactory);
        var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        return new SqlServerCommandLease(new SqlCommand(sql, connection), connection);
    }

    public async ValueTask DisposeAsync()
    {
        Command.Dispose();
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
