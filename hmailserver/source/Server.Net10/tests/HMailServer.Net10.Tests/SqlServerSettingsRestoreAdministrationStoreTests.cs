using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerSettingsRestoreAdministrationStoreTests
{
    [TestMethod]
    public void RestoreSettingsPropertySql_UpdatesExistingRowsWithParametersOnly()
    {
        var sql = SqlServerSettingsRestoreAdministrationStore.RestoreSettingsPropertySql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "settingstring = @StringValue");
        StringAssert.Contains(sql, "settinginteger = @LongValue");
        StringAssert.Contains(sql, "WHERE settingname = @Name");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DROP", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }
}
