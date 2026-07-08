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
}
