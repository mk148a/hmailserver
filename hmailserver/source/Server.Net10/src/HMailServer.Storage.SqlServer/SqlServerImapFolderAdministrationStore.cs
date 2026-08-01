using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerImapFolderAdministrationStore :
    IImapFolderAdministrationStore,
    IImapFolderPermissionAdministrationStore,
    IImapFolderAdministrationMutationStore,
    IImapFolderAdministrationDeletionStore
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

    public const string DeleteFolderPermissionSql = """
DELETE FROM hm_acl
WHERE aclid = @PermissionID
  AND aclsharefolderid = @FolderID
  AND EXISTS
  (
      SELECT 1
      FROM hm_imapfolders
      WHERE folderid = @FolderID
        AND folderaccountid = 0
  );
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

    public const string UpdateFolderSql = """
UPDATE hm_imapfolders
SET folderaccountid = @AccountID,
    folderparentid = @ParentFolderID,
    foldername = @FolderName,
    folderissubscribed = @FolderIsSubscribed
WHERE folderid = @FolderID
  AND folderaccountid = @AccountID
  AND folderparentid = @ParentFolderID;
""";

    public const string DeleteFolderSql = """
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Folders TABLE
(
    folderid int NOT NULL PRIMARY KEY,
    folderaccountid int NOT NULL,
    folderparentid int NOT NULL,
    foldername nvarchar(255) NOT NULL
);

INSERT INTO @Folders (folderid, folderaccountid, folderparentid, foldername)
SELECT folderid, folderaccountid, folderparentid, foldername
FROM hm_imapfolders WITH (UPDLOCK, HOLDLOCK)
WHERE folderid = @FolderID
  AND folderaccountid = @AccountID
  AND folderparentid = @ParentFolderID;

;WITH FolderTree AS
(
    SELECT folderid, folderaccountid, folderparentid, foldername
    FROM @Folders

    UNION ALL

    SELECT child.folderid, child.folderaccountid, child.folderparentid, child.foldername
    FROM hm_imapfolders AS child WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN FolderTree AS parent
        ON child.folderparentid = parent.folderid
    WHERE child.folderaccountid = @AccountID
)
INSERT INTO @Folders (folderid, folderaccountid, folderparentid, foldername)
SELECT tree.folderid, tree.folderaccountid, tree.folderparentid, tree.foldername
FROM FolderTree AS tree
WHERE NOT EXISTS
(
    SELECT 1
    FROM @Folders AS existing
    WHERE existing.folderid = tree.folderid
)
OPTION (MAXRECURSION 32767);

DECLARE @RemovedMessages TABLE
(
    messageid bigint NOT NULL PRIMARY KEY,
    messagefilename nvarchar(255) NOT NULL,
    messageaccountid int NOT NULL,
    messagefolderid int NOT NULL,
    accountaddress nvarchar(255) NULL,
    messagetype tinyint NOT NULL
);

INSERT INTO @RemovedMessages
    (messageid, messagefilename, messageaccountid, messagefolderid, accountaddress, messagetype)
SELECT messages.messageid,
       messages.messagefilename,
       messages.messageaccountid,
       messages.messagefolderid,
       accounts.accountaddress,
       messages.messagetype
FROM hm_messages AS messages WITH (UPDLOCK, HOLDLOCK)
INNER JOIN @Folders AS folders
    ON folders.folderid = messages.messagefolderid
LEFT JOIN hm_accounts AS accounts
    ON accounts.accountid = messages.messageaccountid
WHERE messages.messageaccountid = @AccountID;

DELETE recipients
FROM hm_messagerecipients AS recipients
INNER JOIN @RemovedMessages AS removed
    ON removed.messageid = recipients.recipientmessageid
WHERE removed.messagetype <> 2;

DELETE queue
FROM hm_message_search_queue AS queue
INNER JOIN @RemovedMessages AS removed
    ON removed.messageid = queue.messageid;

DELETE documents
FROM hm_message_search_documents AS documents
INNER JOIN @RemovedMessages AS removed
    ON removed.messageid = documents.messageid;

DELETE metadata
FROM hm_message_metadata AS metadata
INNER JOIN @RemovedMessages AS removed
    ON removed.messageid = metadata.metadata_messageid
WHERE removed.messagetype = 2;

DELETE permissions
FROM hm_acl AS permissions
INNER JOIN @Folders AS folders
    ON folders.folderid = permissions.aclsharefolderid
WHERE folders.folderaccountid = 0;

DELETE messages
FROM hm_messages AS messages
INNER JOIN @RemovedMessages AS removed
    ON removed.messageid = messages.messageid;

DECLARE @RootInbox bit =
    CASE WHEN EXISTS
    (
        SELECT 1
        FROM @Folders
        WHERE folderid = @FolderID
          AND folderparentid = -1
          AND UPPER(foldername) = N'INBOX'
    ) THEN 1 ELSE 0 END;

DELETE folders
FROM hm_imapfolders AS folders
INNER JOIN @Folders AS selected
    ON selected.folderid = folders.folderid
WHERE NOT (folders.folderid = @FolderID AND @RootInbox = 1);

DECLARE @DeletedFolders int;
SET @DeletedFolders = @@ROWCOUNT;

DECLARE @Succeeded bit =
    CASE WHEN @RootInbox = 1 OR @DeletedFolders > 0 THEN 1 ELSE 0 END;

IF @Succeeded = 1
    COMMIT TRANSACTION;
ELSE
    ROLLBACK TRANSACTION;

SELECT @Succeeded;

SELECT messagefilename, messageaccountid, messagefolderid, accountaddress, messagetype
FROM @RemovedMessages
ORDER BY messageid;
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

    public async ValueTask<bool> DeleteFolderPermissionAsync(
        int folderId,
        int permissionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteFolderPermissionSql, connection);
        command.Parameters.Add("@FolderID", SqlDbType.Int).Value = folderId;
        command.Parameters.Add("@PermissionID", SqlDbType.Int).Value = permissionId;

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
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

    public async ValueTask<bool> UpdateFolderAsync(
        ImapFolderAdministrationSnapshot folder,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateFolderSql, connection);
        command.Parameters.Add("@FolderID", SqlDbType.Int).Value = folder.Id;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = folder.AccountId;
        command.Parameters.Add("@ParentFolderID", SqlDbType.Int).Value = folder.ParentId;
        command.Parameters.Add("@FolderName", SqlDbType.NVarChar, 255).Value = folder.Name;
        command.Parameters.Add("@FolderIsSubscribed", SqlDbType.Int).Value = folder.Subscribed ? 1 : 0;

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async ValueTask<ImapFolderAdministrationDeletionResult> DeleteFolderAsync(
        ImapFolderAdministrationSnapshot folder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folder);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteFolderSql, connection);
        command.Parameters.Add("@FolderID", SqlDbType.Int).Value = folder.Id;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = folder.AccountId;
        command.Parameters.Add("@ParentFolderID", SqlDbType.Int).Value = folder.ParentId;

        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new ImapFolderAdministrationDeletionResult(false, Array.Empty<ImapFolderAdministrationDeletedMessage>());
        }

        var succeeded = reader.GetBoolean(0);
        if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
        {
            return new ImapFolderAdministrationDeletionResult(succeeded, Array.Empty<ImapFolderAdministrationDeletedMessage>());
        }

        var messages = new List<ImapFolderAdministrationDeletedMessage>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            messages.Add(
                new ImapFolderAdministrationDeletedMessage(
                    reader.GetString(0),
                    reader.GetInt32(1),
                    reader.GetInt32(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetByte(4)));
        }

        return new ImapFolderAdministrationDeletionResult(succeeded, messages);
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
