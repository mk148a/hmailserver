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
