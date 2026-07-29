using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerImapFolderAdministrationStore : IImapFolderAdministrationStore
{
    public const string GetFoldersForAccountSql = """
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
ORDER BY folderid ASC;
""";

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

    public const string GetChildFoldersSql = """
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
  AND folderparentid = @ParentFolderID
ORDER BY folderid ASC;
""";

    public const string GetFolderPermissionsSql = """
SELECT
    aclid,
    aclsharefolderid,
    aclpermissiontype,
    aclpermissiongroupid,
    aclpermissionaccountid,
    aclvalue
FROM hm_acl
WHERE aclsharefolderid = @FolderID
ORDER BY aclid ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerImapFolderAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetFoldersForAccountAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetFoldersForAccountSql, connection);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;

        return await ReadFoldersAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetRootFoldersAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetRootFoldersSql, connection);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;

        return await ReadFoldersAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetChildFoldersAsync(
        int parentFolderId,
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetChildFoldersSql, connection);
        command.Parameters.Add("@ParentFolderID", SqlDbType.Int).Value = parentFolderId;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;

        return await ReadFoldersAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>> GetFolderPermissionsAsync(
        int folderId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetFolderPermissionsSql, connection);
        command.Parameters.Add("@FolderID", SqlDbType.Int).Value = folderId;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var permissions = new List<ImapFolderPermissionAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            permissions.Add(
                new ImapFolderPermissionAdministrationSnapshot(
                    Id: Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                    ShareFolderId: Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                    PermissionType: Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                    PermissionGroupId: Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                    PermissionAccountId: Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                    Value: Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture)));
        }

        return permissions;
    }

    private static async ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> ReadFoldersAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
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
