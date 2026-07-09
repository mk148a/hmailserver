using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerServerMessageAdministrationStore : IServerMessageAdministrationStore
{
    public const string GetServerMessagesSql = """
SELECT
    smid,
    smname,
    smtext
FROM hm_servermessages
ORDER BY smname ASC;
""";

    public const string UpdateServerMessageSql = """
UPDATE hm_servermessages
SET smname = @name,
    smtext = @text
WHERE smid = @id;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerServerMessageAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<ServerMessageAdministrationSnapshot>> GetServerMessagesAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetServerMessagesSql, connection);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var messages = new List<ServerMessageAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            messages.Add(
                new ServerMessageAdministrationSnapshot(
                    Id: Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Name: reader.GetString(1),
                    Text: reader.GetString(2)));
        }

        return messages;
    }

    public async ValueTask UpdateServerMessageAsync(
        ServerMessageAdministrationSnapshot message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateServerMessageSql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = message.Id;
        command.Parameters.Add("@name", SqlDbType.NVarChar, 255).Value = message.Name;
        command.Parameters.Add("@text", SqlDbType.NVarChar, -1).Value = message.Text;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
