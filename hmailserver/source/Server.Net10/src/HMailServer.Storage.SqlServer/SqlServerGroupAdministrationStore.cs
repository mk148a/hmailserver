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

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerGroupAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<GroupAdministrationSnapshot>> GetGroupsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetGroupsSql, connection);
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

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertGroupSql, connection);
        command.Parameters.Add("@name", SqlDbType.NVarChar, 255).Value = group.Name;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, CultureInfo.InvariantCulture);
    }
}
