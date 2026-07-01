using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerDeliveryQueueAdministrationStoreTests
{
    [TestMethod]
    public void ResetDeliveryTimeSql_PreservesNarrowLegacyMutationBoundary()
    {
        var sql = SqlServerDeliveryQueueAdministrationStore.ResetDeliveryTimeSql;

        StringAssert.Contains(sql, "UPDATE hm_messages");
        StringAssert.Contains(sql, "messagenexttrytime = DATEADD(MINUTE, -1, SYSUTCDATETIME())");
        StringAssert.Contains(sql, "messagetype = 1");
        StringAssert.Contains(sql, "messageid = @MessageId");
        StringAssert.Contains(sql, "messagetype IN (1, 3)");
        Assert.IsFalse(sql.Contains("messagecurnooftries", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("messagelocked", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("messagelease", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_messagerecipients", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
    }
}
