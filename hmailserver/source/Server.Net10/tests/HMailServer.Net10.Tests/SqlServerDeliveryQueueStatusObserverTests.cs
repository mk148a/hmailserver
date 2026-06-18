using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerDeliveryQueueStatusObserverTests
{
    [TestMethod]
    public void InsertStatusSql_WritesDurableDeliveryQueueStatusShape()
    {
        var sql = SqlServerDeliveryQueueStatusObserver.InsertStatusSql;

        StringAssert.Contains(sql, "INSERT INTO hm_delivery_queue_status");
        StringAssert.Contains(sql, "SYSUTCDATETIME()");
        StringAssert.Contains(sql, "eventkind");
        StringAssert.Contains(sql, "leaseowner");
        StringAssert.Contains(sql, "targetkey");
        StringAssert.Contains(sql, "targetdomainname");
        StringAssert.Contains(sql, "targetkind");
        StringAssert.Contains(sql, "recipientcount");
        StringAssert.Contains(sql, "retrycount");
        StringAssert.Contains(sql, "retrydelaymilliseconds");
        StringAssert.Contains(sql, "failurekind");
        StringAssert.Contains(sql, "description");
        StringAssert.Contains(sql, "@RetryDelayMilliseconds");
    }
}
