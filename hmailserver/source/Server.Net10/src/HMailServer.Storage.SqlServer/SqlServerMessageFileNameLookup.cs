using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerMessageFileNameLookup : IMessageFileNameLookup
{
    public const string GetFileNameByMessageIdSql = """
SELECT messagefilename
FROM hm_messages
WHERE messageid = @MessageID;
""";

    public const string GetMessageIdByFileNameSql = """
SELECT messageid
FROM hm_messages
WHERE messagefilename = @FileName;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerMessageFileNameLookup(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<string> GetFileNameByMessageIdAsync(
        long messageId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = new SqlCommand(GetFileNameByMessageIdSql, connection);
        command.Parameters.Add("@MessageID", SqlDbType.BigInt).Value = messageId;

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is null or DBNull
            ? string.Empty
            : Convert.ToString(result, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public async ValueTask<long?> GetMessageIdByFileNameAsync(
        string fileName,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = new SqlCommand(GetMessageIdByFileNameSql, connection);
        command.Parameters.Add("@FileName", SqlDbType.NVarChar, 255).Value = fileName;

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is null or DBNull
            ? null
            : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }
}
