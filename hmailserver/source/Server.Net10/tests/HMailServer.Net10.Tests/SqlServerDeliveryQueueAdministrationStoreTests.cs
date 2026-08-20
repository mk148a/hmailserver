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

    [TestMethod]
    public void RemoveSql_DeletesOnlyUnlockedOrExpiredQueueRowsAndRecipients()
    {
        var sql = SqlServerDeliveryQueueAdministrationStore.RemoveSql;

        StringAssert.Contains(sql, "SET XACT_ABORT ON");
        StringAssert.Contains(sql, "BEGIN TRANSACTION");
        StringAssert.Contains(sql, "WITH (UPDLOCK, READPAST, ROWLOCK)");
        StringAssert.Contains(sql, "messageid = @MessageId");
        StringAssert.Contains(sql, "messagetype IN (1, 3)");
        StringAssert.Contains(sql, "messagelocked = 1");
        StringAssert.Contains(sql, "messageleaseowner IS NOT NULL");
        StringAssert.Contains(sql, "messageleaseexpiresutc > SYSUTCDATETIME()");
        StringAssert.Contains(sql, "DELETE FROM hm_messagerecipients");
        StringAssert.Contains(sql, "recipientmessageid = @MessageId");
        StringAssert.Contains(sql, "DELETE FROM hm_messages");
        StringAssert.Contains(sql, "SELECT @MessageFileName");
        StringAssert.Contains(sql, "COMMIT TRANSACTION");
        Assert.IsTrue(
            sql.IndexOf("DELETE FROM hm_messagerecipients", StringComparison.OrdinalIgnoreCase) <
            sql.IndexOf("DELETE FROM hm_messages", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE hm_messages", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ClearBatchSql_DeletesOnlyTypeOneUnlockedOrExpiredQueueRowsAndRecipients()
    {
        var sql = SqlServerDeliveryQueueAdministrationStore.ClearBatchSql;

        StringAssert.Contains(sql, "SET XACT_ABORT ON");
        StringAssert.Contains(sql, "SELECT TOP (@BatchSize) messageid");
        StringAssert.Contains(sql, "WITH (UPDLOCK, READPAST, ROWLOCK)");
        StringAssert.Contains(sql, "WHERE messagetype = 1");
        StringAssert.Contains(sql, "messagecreatetime <= @ClearStartedUtc");
        StringAssert.Contains(sql, "WHERE messages.messagetype = 1");
        Assert.IsFalse(sql.Contains("messagetype IN (1, 3)", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(sql, "messagelocked = 1");
        StringAssert.Contains(sql, "messageleaseowner IS NOT NULL");
        StringAssert.Contains(sql, "messageleaseexpiresutc > SYSUTCDATETIME()");
        StringAssert.Contains(sql, "ORDER BY messageid");
        StringAssert.Contains(sql, "DELETE recipients");
        StringAssert.Contains(sql, "DELETE messages");
        StringAssert.Contains(sql, "deleted.messagefilename");
        StringAssert.Contains(sql, "COMMIT TRANSACTION");
        Assert.IsTrue(
            sql.IndexOf("DELETE recipients", StringComparison.OrdinalIgnoreCase) <
            sql.IndexOf("DELETE messages", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE hm_messages", StringComparison.OrdinalIgnoreCase));
    }
}
