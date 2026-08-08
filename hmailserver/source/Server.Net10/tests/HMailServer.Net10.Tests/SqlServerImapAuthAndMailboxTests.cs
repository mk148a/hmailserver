using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImapAuthAndMailboxTests
{
    [TestMethod]
    public void AccountLookupSql_RequiresActiveAccountAndDomain()
    {
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "FROM hm_accounts AS a");
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "INNER JOIN hm_domains AS d");
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "a.accountactive <> 0");
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "d.domainactive <> 0");
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "a.accountdomainid");
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "a.accountaddomain");
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "a.accountadusername");
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "a.accountmaxsize");
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "a.accountpersonfirstname");
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "a.accountpersonlastname");
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "a.accountadminlevel");
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "a.accountvacationmessageon");
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "a.accountforwardenabled");
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "a.accountenablesignature");
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "a.accountlastlogontime");
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "CONVERT(int, a.accountadminlevel)");
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "CONVERT(varchar(10), a.accountvacationexpiredate, 23)");
        StringAssert.Contains(SqlServerImapAccountAuthenticator.AccountLookupSql, "CONVERT(varchar(30), a.accountlastlogontime, 126)");
    }

    [TestMethod]
    public void MailboxSql_ResolvesNestedFoldersAndAcl()
    {
        StringAssert.Contains(SqlServerImapMailboxStore.FindChildFolderSql, "FROM hm_imapfolders");
        StringAssert.Contains(SqlServerImapMailboxStore.FindChildFolderSql, "folderparentid = @ParentFolderId");
        StringAssert.Contains(SqlServerImapMailboxStore.SelectMailboxCountersSql, "COUNT_BIG(m.messageid)");
        StringAssert.Contains(SqlServerImapMailboxStore.SelectMailboxCountersSql, "unseencount");
        StringAssert.Contains(SqlServerImapMailboxStore.ListFoldersSql, "folderissubscribed");
        StringAssert.Contains(SqlServerImapMailboxStore.SelectAclPermissionsSql, "FROM hm_acl");
        StringAssert.Contains(SqlServerImapMailboxStore.IsGroupMemberSql, "FROM hm_group_members");
        StringAssert.Contains(SqlServerImapMailboxStore.SelectAclEntriesSql, "LEFT JOIN hm_accounts AS a");
        StringAssert.Contains(SqlServerImapMailboxStore.SelectAclEntriesSql, "LEFT JOIN hm_groups AS g");
        StringAssert.Contains(SqlServerImapMailboxStore.UpsertAclSql, "UPDATE hm_acl");
        StringAssert.Contains(SqlServerImapMailboxStore.DeleteAclSql, "DELETE FROM hm_acl");
    }

    [TestMethod]
    public void ParseMailboxPath_HandlesNestedPrivateAndPublicPaths()
    {
        var privatePath = SqlServerImapMailboxPath.Parse(
            "Projects.2026.Invoices",
            ".",
            "#Public");
        Assert.IsNotNull(privatePath);
        Assert.IsFalse(privatePath.IsPublicFolder);
        CollectionAssert.AreEqual(
            new[] { "Projects", "2026", "Invoices" },
            privatePath.Segments.ToArray());

        var publicPath = SqlServerImapMailboxPath.Parse(
            "#Public.Shared.Invoices",
            ".",
            "#Public");
        Assert.IsNotNull(publicPath);
        Assert.IsTrue(publicPath.IsPublicFolder);
        CollectionAssert.AreEqual(
            new[] { "Shared", "Invoices" },
            publicPath.Segments.ToArray());
        Assert.IsNull(SqlServerImapMailboxPath.Parse("#Public", ".", "#Public"));
    }
}
