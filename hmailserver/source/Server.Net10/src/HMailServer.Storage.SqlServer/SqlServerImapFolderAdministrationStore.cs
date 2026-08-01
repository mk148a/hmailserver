using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerImapFolderAdministrationStore : IImapFolderAdministrationStore, IImapFolderAdministrationMutationStore
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

    public const string InsertFolderSql = """
DECLARE @CreationTime datetime = GETDATE();
INSERT INTO hm_imapfolders
    (folderaccountid, folderparentid, foldername, folderissubscribed, foldercurrentuid, foldercreationtime)
VALUES
    (@AccountID, @ParentFolderID, @FolderName, @FolderIsSubscribed, 0, @CreationTime);
SELECT
    CONVERT(int, SCOPE_IDENTITY()), @AccountID, @ParentFolderID, @FolderName,
    @FolderIsSubscribed, 0, CONVERT(varchar(19), @CreationTime, 120);
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

    public async ValueTask<ImapFolderAdministrationSnapshot> InsertFolderAsync(
        int accountId,
        int parentFolderId,
        string encodedName,
        bool subscribed,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertFolderSql, connection);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@ParentFolderID", SqlDbType.Int).Value = parentFolderId;
        command.Parameters.Add("@FolderName", SqlDbType.NVarChar, 255).Value = encodedName;
        command.Parameters.Add("@FolderIsSubscribed", SqlDbType.Int).Value = subscribed ? 1 : 0;

        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SingleRow,
            cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The IMAP folder insert did not return its generated row.");
        }

        return new ImapFolderAdministrationSnapshot(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetString(3),
            Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) == 1,
            reader.GetInt32(5),
            reader.GetString(6));
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
