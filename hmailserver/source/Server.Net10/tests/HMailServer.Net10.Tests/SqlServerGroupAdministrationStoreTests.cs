using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerGroupAdministrationStoreTests
{
    [TestMethod]
    public void GetGroupsSql_UsesLegacyGroupTableColumnsAndNameOrdering()
    {
        var sql = SqlServerGroupAdministrationStore.GetGroupsSql;

        StringAssert.Contains(sql, "FROM hm_groups");
        StringAssert.Contains(sql, "groupid");
        StringAssert.Contains(sql, "groupname");
        StringAssert.Contains(sql, "ORDER BY groupname ASC");
        Assert.IsFalse(sql.Contains(" JOIN ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InsertGroupSql_UsesParameterizedIdentityReturningInsert()
    {
        var sql = SqlServerGroupAdministrationStore.InsertGroupSql;

        StringAssert.Contains(sql, "INSERT INTO hm_groups");
        StringAssert.Contains(sql, "groupname");
        StringAssert.Contains(sql, "OUTPUT INSERTED.groupid");
        StringAssert.Contains(sql, "@name");
        Assert.IsFalse(sql.Contains("groupname =", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateGroupSql_UsesParameterizedIdentityPredicate()
    {
        var sql = SqlServerGroupAdministrationStore.UpdateGroupSql;

        StringAssert.Contains(sql, "UPDATE hm_groups");
        StringAssert.Contains(sql, "SET groupname = @name");
        StringAssert.Contains(sql, "WHERE groupid = @id");
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN ", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteGroupSql_UsesParameterizedIdentityAndOwnedAclCleanup()
    {
        var groupSql = SqlServerGroupAdministrationStore.DeleteGroupSql;
        var aclSql = SqlServerGroupAdministrationStore.DeleteOwnedGroupAclSql;

        StringAssert.Contains(groupSql, "DELETE FROM hm_groups");
        StringAssert.Contains(groupSql, "WHERE groupid = @id");
        StringAssert.Contains(aclSql, "DELETE FROM hm_acl");
        StringAssert.Contains(aclSql, "aclpermissiontype = 1");
        StringAssert.Contains(aclSql, "aclpermissiongroupid = @id");
        Assert.IsFalse(groupSql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(aclSql.Contains("WHERE groupid = ", StringComparison.OrdinalIgnoreCase));
    }
}
