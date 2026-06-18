using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerDeliveryQueueStatusMaintenanceStoreTests
{
    [TestMethod]
    public void DeleteExpiredStatusesSql_RemovesOldStatusRowsInBatches()
    {
        var sql = SqlServerDeliveryQueueStatusMaintenanceStore.DeleteExpiredStatusesSql;

        StringAssert.Contains(sql, "SELECT TOP (@BatchSize)");
        StringAssert.Contains(sql, "FROM hm_delivery_queue_status");
        StringAssert.Contains(sql, "WHERE eventutc < @CutoffUtc");
        StringAssert.Contains(sql, "ORDER BY eventutc ASC, statusid ASC");
        StringAssert.Contains(sql, "DELETE statusRows");
        StringAssert.Contains(sql, "SELECT @@ROWCOUNT");
    }
}
