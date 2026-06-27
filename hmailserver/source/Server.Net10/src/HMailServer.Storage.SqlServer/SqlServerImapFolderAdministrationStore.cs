using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerImapFolderAdministrationStore : IImapFolderAdministrationStore
{
    public const string GetRootFoldersSql = """
SELECT
    folderid,
    folderaccountid,
    folderparentid,
    foldername,
    folderissubscribed,
    foldercurrentuid,
    CONVERT(varchar(19), foldercreationtime, 120) AS foldercreationtime
FROM hm_imapfolders
WHERE folderaccountid = @AccountID
  AND folderparentid = -1
ORDER BY folderid ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerImapFolderAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetRootFoldersAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetRootFoldersSql, connection);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var folders = new List<ImapFolderAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            folders.Add(
                new ImapFolderAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    AccountId: reader.GetInt32(1),
                    ParentId: reader.GetInt32(2),
                    Name: reader.GetString(3),
                    Subscribed: Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) == 1,
                    CurrentUid: unchecked((int)(uint)reader.GetInt64(5)),
                    CreationTime: reader.GetString(6)));
        }

        return folders;
    }
}
