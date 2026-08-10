using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerImapFolderAdministrationStore :
    IImapFolderAdministrationStore,
    IImapFolderPermissionAdministrationStore,
    IImapFolderPermissionAdministrationMutationStore,
    IImapFolderAdministrationMutationStore,
    IImapFolderAdministrationRestoreStore,
    IImapFolderAdministrationDeletionStore,
    IImapFolderAdministrationRestoreDeletionStore
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

    public const string InsertFolderPermissionSql = """
INSERT INTO hm_acl
    (aclsharefolderid, aclpermissiontype, aclpermissiongroupid, aclpermissionaccountid, aclvalue)
SELECT
    @FolderID, @PermissionType, @PermissionGroupID, @PermissionAccountID, @Value
WHERE EXISTS
(
    SELECT 1
    FROM hm_imapfolders
    WHERE folderid = @FolderID
      AND folderaccountid = 0
);

IF @@ROWCOUNT = 1
BEGIN
    SELECT
        CONVERT(bigint, SCOPE_IDENTITY()),
        CONVERT(bigint, @FolderID),
        CONVERT(int, @PermissionType),
        CONVERT(bigint, @PermissionGroupID),
        CONVERT(bigint, @PermissionAccountID),
        CONVERT(bigint, @Value);
END;
""";

    public const string UpdateFolderPermissionSql = """
UPDATE hm_acl
SET aclsharefolderid = @FolderID,
    aclpermissiontype = @PermissionType,
    aclpermissiongroupid = @PermissionGroupID,
    aclpermissionaccountid = @PermissionAccountID,
    aclvalue = @Value
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

    public const string InsertFolderForRestoreSql = """
INSERT INTO hm_imapfolders
    (folderaccountid, folderparentid, foldername, folderissubscribed, foldercurrentuid, foldercreationtime)
VALUES
    (@AccountID, @ParentFolderID, @FolderName, @FolderIsSubscribed, @CurrentUID, @CreationTime);
SELECT
    CONVERT(int, SCOPE_IDENTITY()), @AccountID, @ParentFolderID, @FolderName,
    @FolderIsSubscribed, @CurrentUID, CONVERT(varchar(19), @CreationTime, 120);
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

    public const string DeleteRestoredFolderTreeSql = """
SET XACT_ABORT ON;
;WITH FolderTree AS
(
    SELECT folderid
    FROM hm_imapfolders
    WHERE folderid = @FolderID
      AND folderaccountid = @AccountID
      AND folderparentid = @ParentFolderID

    UNION ALL

    SELECT child.folderid
    FROM hm_imapfolders AS child
    INNER JOIN FolderTree AS parent
        ON child.folderparentid = parent.folderid
    WHERE child.folderaccountid = @AccountID
)
DELETE folders
FROM hm_imapfolders AS folders
INNER JOIN FolderTree AS tree
    ON tree.folderid = folders.folderid;
SELECT @@ROWCOUNT;
""";

    public const string DeleteAllPublicFoldersForRestoreSql = """
SET XACT_ABORT ON;

DECLARE @FolderIds TABLE
(
    folderid int NOT NULL PRIMARY KEY
);

INSERT INTO @FolderIds (folderid)
SELECT folderid
FROM hm_imapfolders WITH (UPDLOCK, HOLDLOCK)
WHERE folderaccountid = 0;

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
INNER JOIN @FolderIds AS folders
    ON folders.folderid = messages.messagefolderid
LEFT JOIN hm_accounts AS accounts
    ON accounts.accountid = messages.messageaccountid
WHERE messages.messageaccountid = 0;

SELECT messagefilename, messageaccountid, messagefolderid, accountaddress, messagetype
FROM @RemovedMessages
ORDER BY messageid;

DELETE recipients
FROM hm_messagerecipients AS recipients
INNER JOIN @RemovedMessages AS messages
    ON messages.messageid = recipients.recipientmessageid
WHERE messages.messagetype <> 2;

DELETE queue
FROM hm_message_search_queue AS queue
INNER JOIN @RemovedMessages AS messages
    ON messages.messageid = queue.messageid;

DELETE documents
FROM hm_message_search_documents AS documents
INNER JOIN @RemovedMessages AS messages
    ON messages.messageid = documents.messageid;

DELETE metadata
FROM hm_message_metadata AS metadata
INNER JOIN @RemovedMessages AS messages
    ON messages.messageid = metadata.metadata_messageid;

DELETE permissions
FROM hm_acl AS permissions
INNER JOIN @FolderIds AS folders
    ON folders.folderid = permissions.aclsharefolderid;

DELETE messages
FROM hm_messages AS messages
INNER JOIN @RemovedMessages AS selected
    ON selected.messageid = messages.messageid;

DELETE folders
FROM hm_imapfolders AS folders
INNER JOIN @FolderIds AS selected
    ON selected.folderid = folders.folderid
WHERE NOT
(
    folders.folderparentid = -1
    AND UPPER(folders.foldername) = N'INBOX'
);
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly SqlServerBackupRestoreTransactionContext? _transactionContext;

    public SqlServerImapFolderAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    internal SqlServerImapFolderAdministrationStore(
        SqlServerBackupRestoreTransactionContext transactionContext)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        _connectionFactory = null!;
        _transactionContext = transactionContext;
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

    public async ValueTask<ImapFolderPermissionAdministrationSnapshot?> InsertFolderPermissionAsync(
        int folderId,
        int permissionType,
        int permissionGroupId,
        int permissionAccountId,
        int value,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertFolderPermissionSql, connection);
        command.Parameters.Add("@FolderID", SqlDbType.BigInt).Value = folderId;
        command.Parameters.Add("@PermissionType", SqlDbType.TinyInt).Value = permissionType;
        command.Parameters.Add("@PermissionGroupID", SqlDbType.BigInt).Value = permissionGroupId;
        command.Parameters.Add("@PermissionAccountID", SqlDbType.BigInt).Value = permissionAccountId;
        command.Parameters.Add("@Value", SqlDbType.BigInt).Value = value;

        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SingleRow,
            cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var id = reader.GetInt64(0);
        if (id <= 0 || id > int.MaxValue)
        {
            throw new InvalidOperationException("The IMAP folder permission insert returned an invalid generated identity.");
        }

        return new ImapFolderPermissionAdministrationSnapshot(
            checked((int)id),
            checked((int)reader.GetInt64(1)),
            reader.GetInt32(2),
            checked((int)reader.GetInt64(3)),
            checked((int)reader.GetInt64(4)),
            checked((int)reader.GetInt64(5)));
    }

    public async ValueTask<bool> UpdateFolderPermissionAsync(
        int folderId,
        int permissionId,
        int permissionType,
        int permissionGroupId,
        int permissionAccountId,
        int value,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateFolderPermissionSql, connection);
        command.Parameters.Add("@FolderID", SqlDbType.BigInt).Value = folderId;
        command.Parameters.Add("@PermissionID", SqlDbType.BigInt).Value = permissionId;
        command.Parameters.Add("@PermissionType", SqlDbType.TinyInt).Value = permissionType;
        command.Parameters.Add("@PermissionGroupID", SqlDbType.BigInt).Value = permissionGroupId;
        command.Parameters.Add("@PermissionAccountID", SqlDbType.BigInt).Value = permissionAccountId;
        command.Parameters.Add("@Value", SqlDbType.BigInt).Value = value;

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

    public async ValueTask<ImapFolderAdministrationSnapshot> InsertFolderForRestoreAsync(
        ImapFolderAdministrationSnapshot folder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (!DateTime.TryParse(
                folder.CreationTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var creationTime))
        {
            throw new FormatException($"Folder creation time '{folder.CreationTime}' is not a valid legacy timestamp.");
        }

        await using var lease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            InsertFolderForRestoreSql,
            cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = folder.AccountId;
        command.Parameters.Add("@ParentFolderID", SqlDbType.Int).Value = folder.ParentId;
        command.Parameters.Add("@FolderName", SqlDbType.NVarChar, 255).Value = folder.Name;
        command.Parameters.Add("@FolderIsSubscribed", SqlDbType.Int).Value = folder.Subscribed ? 1 : 0;
        command.Parameters.Add("@CurrentUID", SqlDbType.Int).Value = folder.CurrentUid;
        command.Parameters.Add("@CreationTime", SqlDbType.DateTime).Value = creationTime;

        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SingleRow,
            cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The restore folder insert did not return its generated row.");
        }

        return new ImapFolderAdministrationSnapshot(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetString(3),
            Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) != 0,
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

    public async ValueTask<bool> DeleteRestoredFolderTreeAsync(
        int accountId,
        int folderId,
        int parentFolderId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteRestoredFolderTreeSql, connection);
        command.Parameters.Add("@FolderID", SqlDbType.Int).Value = folderId;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@ParentFolderID", SqlDbType.Int).Value = parentFolderId;
        var deleted = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return deleted is not null && Convert.ToInt32(deleted, CultureInfo.InvariantCulture) > 0;
    }

    internal async ValueTask DeleteAllPublicFoldersForRestoreAsync(
        CancellationToken cancellationToken)
    {
        if (_transactionContext is null)
        {
            throw new InvalidOperationException(
                "Public-folder restore cleanup requires an existing SQL restore transaction.");
        }

        await using var commandLease = await SqlServerCommandLease
            .OpenAsync(
                _connectionFactory,
                _transactionContext,
                DeleteAllPublicFoldersForRestoreSql,
                cancellationToken)
            .ConfigureAwait(false);

        await commandLease.Command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async ValueTask<IReadOnlyList<ImapFolderAdministrationDeletedMessage>>
        DeleteAllPublicFoldersForRestoreWithManifestAsync(CancellationToken cancellationToken)
    {
        if (_transactionContext is null)
        {
            throw new InvalidOperationException(
                "Public-folder restore cleanup requires an existing SQL restore transaction.");
        }

        await using var commandLease = await SqlServerCommandLease
            .OpenAsync(
                _connectionFactory,
                _transactionContext,
                DeleteAllPublicFoldersForRestoreSql,
                cancellationToken)
            .ConfigureAwait(false);

        await using var reader = await commandLease.Command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

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

        while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
            }
        }

        return messages;
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
                    CurrentUid: unchecked((int)Convert.ToUInt32(reader.GetValue(5), CultureInfo.InvariantCulture)),
                    CreationTime: reader.GetString(6)));
        }

        return folders;
    }
}
