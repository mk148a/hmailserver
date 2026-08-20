using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerGroupAdministrationStore : IGroupAdministrationStore
{
    public const string GetGroupsSql = """
SELECT
    groupid,
    groupname
FROM hm_groups
ORDER BY groupname ASC;
""";

    public const string InsertGroupSql = """
INSERT INTO hm_groups
    (groupname)
OUTPUT INSERTED.groupid
VALUES (@name);
""";

    public const string UpdateGroupSql = """
UPDATE hm_groups
SET groupname = @name
WHERE groupid = @id;
""";

    public const string DeleteGroupSql = """
DELETE FROM hm_groups
WHERE groupid = @id;
""";

    public const string DeleteOwnedGroupAclSql = """
DELETE FROM hm_acl
WHERE aclpermissiontype = 1
  AND aclpermissiongroupid = @id;
""";

    public const string DeleteAllGroupsForRestoreSql = """
DELETE FROM hm_acl
WHERE aclpermissiontype = 1
  AND EXISTS
  (
      SELECT 1
      FROM hm_groups
      WHERE groupid = hm_acl.aclpermissiongroupid
  );

DELETE FROM hm_groups;
""";

    private readonly SqlServerConnectionFactory? _connectionFactory;
    private readonly SqlServerBackupRestoreTransactionContext? _transactionContext;

    public SqlServerGroupAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    internal SqlServerGroupAdministrationStore(SqlServerBackupRestoreTransactionContext transactionContext)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        _transactionContext = transactionContext;
    }

    public async ValueTask<IReadOnlyList<GroupAdministrationSnapshot>> GetGroupsAsync(
        CancellationToken cancellationToken)
    {
        await using var lease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            GetGroupsSql,
            cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var groups = new List<GroupAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            groups.Add(
                new GroupAdministrationSnapshot(
                    Id: Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Name: reader.GetString(1)));
        }

        return groups;
    }

    public async ValueTask<int> InsertGroupAsync(
        GroupAdministrationSnapshot group,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);

        await using var lease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            InsertGroupSql,
            cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        command.Parameters.Add("@name", SqlDbType.NVarChar, 255).Value = group.Name;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, CultureInfo.InvariantCulture);
    }

    public async ValueTask<bool> UpdateGroupAsync(
        GroupAdministrationSnapshot group,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);

        await using var lease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            UpdateGroupSql,
            cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        command.Parameters.Add("@name", SqlDbType.NVarChar, 255).Value = group.Name;
        command.Parameters.Add("@id", SqlDbType.Int).Value = group.Id;
        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affectedRows == 1;
    }

    public async ValueTask<bool> DeleteGroupByIdAsync(
        int groupId,
        CancellationToken cancellationToken)
    {
        if (_transactionContext is not null)
        {
            await using var groupCommand = new SqlCommand(DeleteGroupSql, _transactionContext.Connection, _transactionContext.Transaction);
            groupCommand.Parameters.Add("@id", SqlDbType.Int).Value = groupId;
            var groupRows = await groupCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await using var aclCommand = new SqlCommand(DeleteOwnedGroupAclSql, _transactionContext.Connection, _transactionContext.Transaction);
            aclCommand.Parameters.Add("@id", SqlDbType.Int).Value = groupId;
            var aclRows = await aclCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return groupRows == 1 || aclRows > 0;
        }

        await using var connection = await _connectionFactory!.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var groupCommand = new SqlCommand(DeleteGroupSql, connection, transaction);
            groupCommand.Parameters.Add("@id", SqlDbType.Int).Value = groupId;
            var groupRows = await groupCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await using var aclCommand = new SqlCommand(DeleteOwnedGroupAclSql, connection, transaction);
            aclCommand.Parameters.Add("@id", SqlDbType.Int).Value = groupId;
            var aclRows = await aclCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return groupRows == 1 || aclRows > 0;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    internal async ValueTask DeleteAllGroupsForRestoreAsync(CancellationToken cancellationToken)
    {
        if (_transactionContext is not null)
        {
            await using var command = new SqlCommand(
                DeleteAllGroupsForRestoreSql,
                _transactionContext.Connection,
                _transactionContext.Transaction);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        throw new NotSupportedException(
            "Group replacement requires a transaction-scoped SQL store.");
    }
}
