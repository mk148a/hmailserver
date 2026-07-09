using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerServerMessageAdministrationStoreTests
{
    [TestMethod]
    public void GetServerMessagesSql_UsesLegacyColumnsAndNameOrdering()
    {
        var sql = SqlServerServerMessageAdministrationStore.GetServerMessagesSql;

        StringAssert.Contains(sql, "FROM hm_servermessages");
        StringAssert.Contains(sql, "smid");
        StringAssert.Contains(sql, "smname");
        StringAssert.Contains(sql, "smtext");
        StringAssert.Contains(sql, "ORDER BY smname ASC");
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateServerMessageSql_UpdatesOnlyMatchingLegacyServerMessageRow()
    {
        var sql = SqlServerServerMessageAdministrationStore.UpdateServerMessageSql;

        StringAssert.Contains(sql, "UPDATE hm_servermessages");
        StringAssert.Contains(sql, "SET smname = @name");
        StringAssert.Contains(sql, "smtext = @text");
        StringAssert.Contains(sql, "WHERE smid = @id");
        Assert.IsFalse(sql.Contains(" JOIN ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT ", StringComparison.OrdinalIgnoreCase));
    }
}
