using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerGroupMemberAdministrationStore : IGroupMemberAdministrationStore
{
    public const string GetGroupMembersSql = """
SELECT
    memberid,
    membergroupid,
    memberaccountid
FROM hm_group_members
WHERE membergroupid = @GroupId
ORDER BY memberid ASC;
""";

    public const string InsertGroupMemberSql = """
INSERT INTO hm_group_members
    (membergroupid, memberaccountid)
OUTPUT INSERTED.memberid
VALUES (@groupId, @accountId);
""";

    public const string UpdateGroupMemberSql = """
UPDATE hm_group_members
SET membergroupid = @groupId,
    memberaccountid = @accountId
WHERE memberid = @memberId
  AND membergroupid = @ownerGroupId;
""";

    public const string DeleteGroupMemberSql = """
DELETE FROM hm_group_members
WHERE memberid = @memberId
  AND membergroupid = @groupId;
""";

    private readonly SqlServerConnectionFactory? _connectionFactory;
    private readonly SqlServerBackupRestoreTransactionContext? _transactionContext;

    public SqlServerGroupMemberAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    internal SqlServerGroupMemberAdministrationStore(SqlServerBackupRestoreTransactionContext transactionContext)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        _transactionContext = transactionContext;
    }

    public async ValueTask<IReadOnlyList<GroupMemberAdministrationSnapshot>> GetGroupMembersAsync(
        int groupId,
        CancellationToken cancellationToken)
    {
        await using var lease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            GetGroupMembersSql,
            cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        command.Parameters.Add("@GroupId", SqlDbType.BigInt).Value = groupId;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var members = new List<GroupMemberAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            members.Add(
                new GroupMemberAdministrationSnapshot(
                    Id: Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                    GroupId: Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                    AccountId: Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture)));
        }

        return members;
    }

    public async ValueTask<int> InsertGroupMemberAsync(
        GroupMemberAdministrationSnapshot member,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);

        await using var lease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            InsertGroupMemberSql,
            cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        command.Parameters.Add("@groupId", SqlDbType.Int).Value = member.GroupId;
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = member.AccountId;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, CultureInfo.InvariantCulture);
    }

    public async ValueTask<bool> UpdateGroupMemberAsync(
        int owningGroupId,
        GroupMemberAdministrationSnapshot member,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);

        await using var lease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            UpdateGroupMemberSql,
            cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        command.Parameters.Add("@groupId", SqlDbType.Int).Value = member.GroupId;
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = member.AccountId;
        command.Parameters.Add("@memberId", SqlDbType.Int).Value = member.Id;
        command.Parameters.Add("@ownerGroupId", SqlDbType.Int).Value = owningGroupId;
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async ValueTask<bool> DeleteGroupMemberByIdAsync(
        int groupId,
        int memberId,
        CancellationToken cancellationToken)
    {
        await using var lease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            DeleteGroupMemberSql,
            cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        command.Parameters.Add("@memberId", SqlDbType.Int).Value = memberId;
        command.Parameters.Add("@groupId", SqlDbType.Int).Value = groupId;
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }
}
