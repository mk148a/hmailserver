using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerGroupMemberAdministrationStoreTests
{
    [TestMethod]
    public void GetGroupMembersSql_UsesLegacyGroupMemberColumnsFilterAndOrdering()
    {
        var sql = SqlServerGroupMemberAdministrationStore.GetGroupMembersSql;

        StringAssert.Contains(sql, "FROM hm_group_members");
        StringAssert.Contains(sql, "memberid");
        StringAssert.Contains(sql, "membergroupid");
        StringAssert.Contains(sql, "memberaccountid");
        StringAssert.Contains(sql, "WHERE membergroupid = @GroupId");
        StringAssert.Contains(sql, "ORDER BY memberid ASC");
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InsertGroupMemberSql_UsesParameterizedOwnerAndAccountIdentity()
    {
        var sql = SqlServerGroupMemberAdministrationStore.InsertGroupMemberSql;

        StringAssert.Contains(sql, "INSERT INTO hm_group_members");
        StringAssert.Contains(sql, "membergroupid");
        StringAssert.Contains(sql, "memberaccountid");
        StringAssert.Contains(sql, "OUTPUT INSERTED.memberid");
        StringAssert.Contains(sql, "@groupId");
        StringAssert.Contains(sql, "@accountId");
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteGroupMemberSql_UsesOwnerAndMemberIdentityPredicates()
    {
        var sql = SqlServerGroupMemberAdministrationStore.DeleteGroupMemberSql;

        StringAssert.Contains(sql, "DELETE FROM hm_group_members");
        StringAssert.Contains(sql, "memberid = @memberId");
        StringAssert.Contains(sql, "membergroupid = @groupId");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateGroupMemberSql_UsesLegacyColumnsAndOwnerScopedIdentityPredicates()
    {
        var sql = SqlServerGroupMemberAdministrationStore.UpdateGroupMemberSql;

        StringAssert.Contains(sql, "UPDATE hm_group_members");
        StringAssert.Contains(sql, "membergroupid = @groupId");
        StringAssert.Contains(sql, "memberaccountid = @accountId");
        StringAssert.Contains(sql, "memberid = @memberId");
        StringAssert.Contains(sql, "membergroupid = @ownerGroupId");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }
}
