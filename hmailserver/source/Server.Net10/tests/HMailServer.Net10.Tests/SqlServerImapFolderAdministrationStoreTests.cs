using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImapFolderAdministrationStoreTests
{
    [TestMethod]
    public void GetFoldersForAccountSql_UsesLegacyAccountScopeAndIdOrderingWithoutRootFilter()
    {
        var sql = SqlServerImapFolderAdministrationStore.GetFoldersForAccountSql;

        AssertFolderProjection(sql);
        StringAssert.Contains(sql, "FROM hm_imapfolders");
        StringAssert.Contains(sql, "folderaccountid = @AccountID");
        Assert.IsFalse(sql.Contains("folderparentid =", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(sql, "ORDER BY folderid ASC");
        AssertNoOutOfScopeFolderAccess(sql);
    }

    [TestMethod]
    public void GetRootFoldersSql_UsesLegacyFolderTableAccountAndRootFiltersAndIdOrdering()
    {
        var sql = SqlServerImapFolderAdministrationStore.GetRootFoldersSql;

        AssertFolderProjection(sql);
        StringAssert.Contains(sql, "FROM hm_imapfolders");
        StringAssert.Contains(sql, "folderaccountid = @AccountID");
        StringAssert.Contains(sql, "folderparentid = -1");
        StringAssert.Contains(sql, "ORDER BY folderid ASC");
        AssertNoOutOfScopeFolderAccess(sql);
    }

    [TestMethod]
    public void GetChildFoldersSql_UsesLegacyFolderTableAccountAndParentFiltersAndIdOrdering()
    {
        var sql = SqlServerImapFolderAdministrationStore.GetChildFoldersSql;

        AssertFolderProjection(sql);
        StringAssert.Contains(sql, "FROM hm_imapfolders");
        StringAssert.Contains(sql, "folderaccountid = @AccountID");
        StringAssert.Contains(sql, "folderparentid = @ParentFolderID");
        StringAssert.Contains(sql, "ORDER BY folderid ASC");
        AssertNoOutOfScopeFolderAccess(sql);
    }

    [TestMethod]
    public void InsertFolderSql_PreservesLegacyUnvalidatedNumericParentShape()
    {
        var sql = SqlServerImapFolderAdministrationStore.InsertFolderSql;

        StringAssert.Contains(sql, "VALUES");
        StringAssert.Contains(sql, "@AccountID");
        StringAssert.Contains(sql, "@ParentFolderID");
        Assert.IsFalse(sql.Contains("FROM hm_imapfolders", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("EXISTS", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void GetFolderPermissionsSql_ReadsOnlyLegacyAclRowsForSelectedFolder()
    {
        var sql = SqlServerImapFolderAdministrationStore.GetFolderPermissionsSql;

        StringAssert.Contains(sql, "aclid");
        StringAssert.Contains(sql, "aclsharefolderid");
        StringAssert.Contains(sql, "aclpermissiontype");
        StringAssert.Contains(sql, "aclpermissiongroupid");
        StringAssert.Contains(sql, "aclpermissionaccountid");
        StringAssert.Contains(sql, "aclvalue");
        StringAssert.Contains(sql, "FROM hm_acl");
        StringAssert.Contains(sql, "aclsharefolderid = @FolderID");
        StringAssert.Contains(sql, "ORDER BY aclid ASC");
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_messages", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteFolderPermissionSql_UsesPermissionFolderAndPublicFolderScopeParameters()
    {
        var sql = SqlServerImapFolderAdministrationStore.DeleteFolderPermissionSql;

        StringAssert.Contains(sql, "DELETE FROM hm_acl");
        StringAssert.Contains(sql, "aclid = @PermissionID");
        StringAssert.Contains(sql, "aclsharefolderid = @FolderID");
        StringAssert.Contains(sql, "FROM hm_imapfolders");
        StringAssert.Contains(sql, "folderid = @FolderID");
        StringAssert.Contains(sql, "folderaccountid = 0");
        StringAssert.Contains(sql, "@PermissionID");
        StringAssert.Contains(sql, "@FolderID");
        Assert.IsFalse(sql.Contains("@AccountID", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InsertFolderPermissionSql_UsesOwningPublicFolderAndTypedIdentityWithoutCallerShareFolderId()
    {
        var sql = SqlServerImapFolderAdministrationStore.InsertFolderPermissionSql;

        StringAssert.Contains(sql, "INSERT INTO hm_acl");
        StringAssert.Contains(sql, "aclsharefolderid");
        StringAssert.Contains(sql, "aclpermissiontype");
        StringAssert.Contains(sql, "aclpermissiongroupid");
        StringAssert.Contains(sql, "aclpermissionaccountid");
        StringAssert.Contains(sql, "aclvalue");
        StringAssert.Contains(sql, "WHERE folderid = @FolderID");
        StringAssert.Contains(sql, "folderaccountid = 0");
        StringAssert.Contains(sql, "SCOPE_IDENTITY");
        StringAssert.Contains(sql, "CONVERT(bigint");
        StringAssert.Contains(sql, "@FolderID");
        StringAssert.Contains(sql, "@PermissionType");
        StringAssert.Contains(sql, "@PermissionGroupID");
        StringAssert.Contains(sql, "@PermissionAccountID");
        StringAssert.Contains(sql, "@Value");
        Assert.IsFalse(sql.Contains("@ShareFolderID", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("MAX(", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("@@IDENTITY", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InsertFolderPermissionForRestoreSql_IsTransactionFriendlyAndFailClosedToPublicFoldersAndLegacyPrincipals()
    {
        var sql = SqlServerImapFolderAdministrationStore.InsertFolderPermissionForRestoreSql;

        StringAssert.Contains(sql, "INSERT INTO hm_acl");
        StringAssert.Contains(sql, "OUTPUT INSERTED.aclid");
        StringAssert.Contains(sql, "folderaccountid = 0");
        StringAssert.Contains(sql, "@PermissionType IN (0, 1, 2)");
        StringAssert.Contains(sql, "@Value BETWEEN 0 AND 2047");
        StringAssert.Contains(sql, "@PermissionType = 0 AND @PermissionGroupID = 0 AND @PermissionAccountID > 0");
        StringAssert.Contains(sql, "@PermissionType = 1 AND @PermissionGroupID > 0 AND @PermissionAccountID = 0");
        StringAssert.Contains(sql, "@PermissionType = 2 AND @PermissionGroupID = 0 AND @PermissionAccountID = 0");
        Assert.IsFalse(sql.Contains("BEGIN TRANSACTION", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("COMMIT", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task InsertFolderPermissionForRestoreAsync_RejectsInvalidPrincipalBeforeOpeningSqlConnection()
    {
        var store = new SqlServerImapFolderAdministrationStore(
            new SqlServerConnectionFactory("Server=invalid-host;Database=invalid;Connect Timeout=1"));

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await store.InsertFolderPermissionForRestoreAsync(
                folderId: 10,
                permissionType: 0,
                permissionGroupId: 20,
                permissionAccountId: 0,
                value: 2,
                CancellationToken.None));
    }

    [TestMethod]
    public async Task InsertFolderPermissionForRestoreAsync_RejectsInvalidRightsBeforeOpeningSqlConnection()
    {
        var store = new SqlServerImapFolderAdministrationStore(
            new SqlServerConnectionFactory("Server=invalid-host;Database=invalid;Connect Timeout=1"));

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(async () =>
            await store.InsertFolderPermissionForRestoreAsync(
                folderId: 10,
                permissionType: 2,
                permissionGroupId: 0,
                permissionAccountId: 0,
                value: 2048,
                CancellationToken.None));
    }

    [TestMethod]
    public void UpdateFolderPermissionSql_UpdatesAllLegacyColumnsWithinAclAndPublicFolderScope()
    {
        var sql = SqlServerImapFolderAdministrationStore.UpdateFolderPermissionSql;

        StringAssert.Contains(sql, "UPDATE hm_acl");
        StringAssert.Contains(sql, "aclsharefolderid = @FolderID");
        StringAssert.Contains(sql, "aclpermissiontype = @PermissionType");
        StringAssert.Contains(sql, "aclpermissiongroupid = @PermissionGroupID");
        StringAssert.Contains(sql, "aclpermissionaccountid = @PermissionAccountID");
        StringAssert.Contains(sql, "aclvalue = @Value");
        StringAssert.Contains(sql, "aclid = @PermissionID");
        StringAssert.Contains(sql, "FROM hm_imapfolders");
        StringAssert.Contains(sql, "folderid = @FolderID");
        StringAssert.Contains(sql, "folderaccountid = 0");
        StringAssert.Contains(sql, "@PermissionID");
        StringAssert.Contains(sql, "@PermissionType");
        StringAssert.Contains(sql, "@PermissionGroupID");
        StringAssert.Contains(sql, "@PermissionAccountID");
        StringAssert.Contains(sql, "@Value");
        Assert.IsFalse(sql.Contains("hm_messages", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UpsertAclSql", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InsertFolderSql_PreservesLegacyFolderColumnsAndGeneratedIdentity()
    {
        var sql = SqlServerImapFolderAdministrationStore.InsertFolderSql;

        StringAssert.Contains(sql, "INSERT INTO hm_imapfolders");
        StringAssert.Contains(sql, "folderaccountid");
        StringAssert.Contains(sql, "folderparentid");
        StringAssert.Contains(sql, "foldername");
        StringAssert.Contains(sql, "folderissubscribed");
        StringAssert.Contains(sql, "foldercurrentuid");
        StringAssert.Contains(sql, "foldercreationtime");
        StringAssert.Contains(sql, "SCOPE_IDENTITY");
        StringAssert.Contains(sql, "@AccountID");
        StringAssert.Contains(sql, "@ParentFolderID");
        StringAssert.Contains(sql, "@FolderName");
        StringAssert.Contains(sql, "@FolderIsSubscribed");
    }

    [TestMethod]
    public void UpdateFolderSql_PreservesLegacyFieldsAndOwnerScope()
    {
        var sql = SqlServerImapFolderAdministrationStore.UpdateFolderSql;

        StringAssert.Contains(sql, "UPDATE hm_imapfolders");
        StringAssert.Contains(sql, "folderaccountid = @AccountID");
        StringAssert.Contains(sql, "folderparentid = @ParentFolderID");
        StringAssert.Contains(sql, "foldername = @FolderName");
        StringAssert.Contains(sql, "folderissubscribed = @FolderIsSubscribed");
        StringAssert.Contains(sql, "folderid = @FolderID");
        StringAssert.Contains(sql, "WHERE");
    }

    [TestMethod]
    public void RenameRootFolderSqlUsesTransactionalRootAndOwnerScope()
    {
        var sql = SqlServerImapMailboxStore.RenameRootFolderSql;

        StringAssert.Contains(sql, "SET XACT_ABORT ON");
        StringAssert.Contains(sql, "BEGIN TRANSACTION");
        StringAssert.Contains(sql, "WITH (UPDLOCK, HOLDLOCK)");
        StringAssert.Contains(sql, "folderaccountid = @AccountID");
        StringAssert.Contains(sql, "folderparentid = -1");
        StringAssert.Contains(sql, "LOWER(foldername) = LOWER(@SourceName)");
        StringAssert.Contains(sql, "LOWER(foldername) = LOWER(@DestinationName)");
        StringAssert.Contains(sql, "foldername = @DestinationName");
        StringAssert.Contains(sql, "COMMIT TRANSACTION");
        Assert.IsFalse(sql.Contains("SCOPE_IDENTITY", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteFolderSql_CleansLegacyDependentsTransactionallyAndPreservesRootInbox()
    {
        var sql = SqlServerImapFolderAdministrationStore.DeleteFolderSql;

        StringAssert.Contains(sql, "SET XACT_ABORT ON");
        StringAssert.Contains(sql, "BEGIN TRANSACTION");
        StringAssert.Contains(sql, "COMMIT TRANSACTION");
        StringAssert.Contains(sql, "ROLLBACK TRANSACTION");
        StringAssert.Contains(sql, "folderid = @FolderID");
        StringAssert.Contains(sql, "folderaccountid = @AccountID");
        StringAssert.Contains(sql, "folderparentid = @ParentFolderID");
        StringAssert.Contains(sql, "WITH (UPDLOCK, HOLDLOCK)");
        StringAssert.Contains(sql, "MAXRECURSION 32767");
        StringAssert.Contains(sql, "hm_messagerecipients");
        StringAssert.Contains(sql, "hm_message_search_queue");
        StringAssert.Contains(sql, "hm_message_search_documents");
        StringAssert.Contains(sql, "hm_message_metadata");
        StringAssert.Contains(sql, "removed.messagetype = 2");
        StringAssert.Contains(sql, "hm_acl");
        StringAssert.Contains(sql, "folders.folderaccountid = 0");
        StringAssert.Contains(sql, "hm_accounts");
        StringAssert.Contains(sql, "messages.messageaccountid = @AccountID");
        StringAssert.Contains(sql, "folderparentid = -1");
        StringAssert.Contains(sql, "UPPER(foldername) = N'INBOX'");
        StringAssert.Contains(sql, "messagefilename");
        StringAssert.Contains(sql, "accountaddress");
    }

    [TestMethod]
    public void DeleteAllForAccountSql_CleansMessagesAndFoldersTransactionallyWithinAccountScope()
    {
        var sql = SqlServerImapFolderAdministrationStore.DeleteAllForAccountSql;

        StringAssert.Contains(sql, "SET XACT_ABORT ON");
        StringAssert.Contains(sql, "BEGIN TRANSACTION");
        StringAssert.Contains(sql, "COMMIT TRANSACTION");
        StringAssert.Contains(sql, "ROLLBACK TRANSACTION");
        StringAssert.Contains(sql, "FROM hm_imapfolders WITH (UPDLOCK, HOLDLOCK)");
        StringAssert.Contains(sql, "WHERE folderaccountid = @AccountID");
        StringAssert.Contains(sql, "accountdomainid = @DomainID");
        StringAssert.Contains(sql, "accountaddress = @AccountAddress");
        StringAssert.Contains(sql, "WHERE messages.messageaccountid = @AccountID");
        StringAssert.Contains(sql, "INNER JOIN @Folders AS folders");
        StringAssert.Contains(sql, "hm_messagerecipients");
        StringAssert.Contains(sql, "hm_message_search_queue");
        StringAssert.Contains(sql, "hm_message_search_documents");
        StringAssert.Contains(sql, "hm_message_metadata");
        StringAssert.Contains(sql, "hm_acl");
        StringAssert.Contains(sql, "messagefilename");
        StringAssert.Contains(sql, "accountaddress");
        StringAssert.Contains(sql, "folderparentid = -1");
        StringAssert.Contains(sql, "UPPER(folders.foldername) = N'INBOX'");
        Assert.IsFalse(sql.Contains("DELETE FROM hm_accounts", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(
            sql.IndexOf("DELETE messages", StringComparison.OrdinalIgnoreCase)
                < sql.IndexOf("DELETE folders", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteAllPublicFoldersForRestoreSql_IsTransactionScopedAndDeletesLegacyDependentsInOrder()
    {
        var sql = SqlServerImapFolderAdministrationStore.DeleteAllPublicFoldersForRestoreSql;

        StringAssert.Contains(sql, "SET XACT_ABORT ON");
        StringAssert.Contains(sql, "FROM hm_imapfolders WITH (UPDLOCK, HOLDLOCK)");
        StringAssert.Contains(sql, "WHERE folderaccountid = 0");
        StringAssert.Contains(sql, "FROM hm_messages AS messages WITH (UPDLOCK, HOLDLOCK)");
        StringAssert.Contains(sql, "WHERE messages.messageaccountid = 0");
        StringAssert.Contains(sql, "messages.messagetype <> 2");
        StringAssert.Contains(sql, "@FolderIds");
        StringAssert.Contains(sql, "@RemovedMessages");
        StringAssert.Contains(sql, "messagefilename");
        StringAssert.Contains(sql, "messageaccountid");
        StringAssert.Contains(sql, "messagefolderid");
        StringAssert.Contains(sql, "accountaddress");
        StringAssert.Contains(sql, "messagetype");
        StringAssert.Contains(sql, "LEFT JOIN hm_accounts AS accounts");
        StringAssert.Contains(sql, "hm_messagerecipients");
        StringAssert.Contains(sql, "hm_message_search_queue");
        StringAssert.Contains(sql, "hm_message_search_documents");
        StringAssert.Contains(sql, "hm_message_metadata");
        StringAssert.Contains(sql, "hm_acl");
        StringAssert.Contains(sql, "DELETE folders");
        StringAssert.Contains(sql, "folders.folderparentid = -1");
        StringAssert.Contains(sql, "UPPER(folders.foldername) = N'INBOX'");
        Assert.IsFalse(sql.Contains("BEGIN TRANSACTION", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("COMMIT TRANSACTION", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("ROLLBACK TRANSACTION", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(
            sql.IndexOf("SELECT messagefilename", StringComparison.OrdinalIgnoreCase)
                < sql.IndexOf("DELETE recipients", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(
            sql.IndexOf("DELETE messages", StringComparison.OrdinalIgnoreCase)
                < sql.IndexOf("DELETE folders", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertFolderProjection(string sql)
    {
        StringAssert.Contains(sql, "folderid");
        StringAssert.Contains(sql, "folderaccountid");
        StringAssert.Contains(sql, "folderparentid");
        StringAssert.Contains(sql, "foldername");
        StringAssert.Contains(sql, "folderissubscribed");
        StringAssert.Contains(sql, "foldercurrentuid");
        StringAssert.Contains(sql, "foldercreationtime");
    }

    private static void AssertNoOutOfScopeFolderAccess(string sql)
    {
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_messages", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_acl", StringComparison.OrdinalIgnoreCase));
    }
}
