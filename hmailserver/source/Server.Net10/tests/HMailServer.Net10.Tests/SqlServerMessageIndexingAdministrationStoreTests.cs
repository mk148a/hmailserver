using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerMessageIndexingAdministrationStoreTests
{
    [TestMethod]
    public void StatusSql_UsesLegacyDeliveredCountAndNet10SearchTables()
    {
        var sql = SqlServerMessageIndexingAdministrationStore.StatusSql;

        StringAssert.Contains(sql, "FROM hm_messages");
        StringAssert.Contains(sql, "messagetype = 2");
        StringAssert.Contains(sql, "FROM hm_message_search_documents");
        StringAssert.Contains(sql, "settingname = N'MessageIndexing'");
        StringAssert.Contains(sql, "FULLTEXTSERVICEPROPERTY('IsFullTextInstalled')");
        StringAssert.Contains(sql, "FROM hm_message_search_queue");
        StringAssert.Contains(sql, "lasterror");
    }

    [TestMethod]
    public void SetEnabledSql_PersistsLegacySettingAndQueuesMissingMessagesWhenEnabled()
    {
        var sql = SqlServerMessageIndexingAdministrationStore.SetEnabledSql;

        StringAssert.Contains(sql, "BEGIN TRANSACTION");
        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "settingname = N'MessageIndexing'");
        StringAssert.Contains(sql, "INSERT INTO hm_settings");
        StringAssert.Contains(sql, "IF @Enabled <> 0");
        StringAssert.Contains(sql, "INSERT INTO hm_message_search_queue");
        StringAssert.Contains(sql, "COMMIT TRANSACTION");
    }

    [TestMethod]
    public void ClearAndIndexSql_PreserveQueueDrivenReindexSemantics()
    {
        StringAssert.Contains(
            SqlServerMessageIndexingAdministrationStore.ClearSql,
            "DELETE FROM hm_message_search_documents");
        StringAssert.Contains(
            SqlServerMessageIndexingAdministrationStore.ClearSql,
            "DELETE FROM hm_message_search_queue");
        StringAssert.Contains(
            SqlServerMessageIndexingAdministrationStore.ClearSql,
            "INSERT INTO hm_message_search_queue");
        StringAssert.Contains(
            SqlServerMessageIndexingAdministrationStore.IndexSql,
            "INSERT INTO hm_message_search_queue");
        StringAssert.Contains(
            SqlServerMessageIndexingAdministrationStore.IndexSql,
            "messagetype = 2");
    }
}
