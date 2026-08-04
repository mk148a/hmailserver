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

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerGroupMemberAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<GroupMemberAdministrationSnapshot>> GetGroupMembersAsync(
        int groupId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetGroupMembersSql, connection);
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

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertGroupMemberSql, connection);
        command.Parameters.Add("@groupId", SqlDbType.Int).Value = member.GroupId;
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = member.AccountId;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, CultureInfo.InvariantCulture);
    }
}
