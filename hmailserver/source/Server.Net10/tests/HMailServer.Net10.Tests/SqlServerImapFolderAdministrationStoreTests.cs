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
