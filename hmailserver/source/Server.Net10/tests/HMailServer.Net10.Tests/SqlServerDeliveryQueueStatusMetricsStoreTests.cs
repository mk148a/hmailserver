using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerDeliveryQueueStatusMetricsStoreTests
{
    [TestMethod]
    public void SelectCountsByKindSql_ReadsRecentStatusCountsByEventKind()
    {
        var sql = SqlServerDeliveryQueueStatusMetricsStore.SelectCountsByKindSql;

        StringAssert.Contains(sql, "COUNT_BIG(*)");
        StringAssert.Contains(sql, "FROM hm_delivery_queue_status");
        StringAssert.Contains(sql, "eventutc >= @SinceUtc");
        StringAssert.Contains(sql, "eventutc < @UntilUtc");
        StringAssert.Contains(sql, "GROUP BY eventkind");
        StringAssert.Contains(sql, "ORDER BY eventkind ASC");
    }
}
