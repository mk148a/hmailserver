using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerMessageAdministrationStoreTests
{
    [TestMethod]
    public void GetAccountMessagesSql_ReadsAllMessagesForSelectedAccountInUidOrder()
    {
        var sql = SqlServerMessageAdministrationStore.GetAccountMessagesSql;

        AssertMessageProjection(sql);
        StringAssert.Contains(sql, "FROM hm_messages");
        StringAssert.Contains(sql, "messageaccountid = @AccountID");
        Assert.IsFalse(sql.Contains("messagetype = 2", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("messagefolderid = @FolderID", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(sql, "ORDER BY messageuid ASC;");
        Assert.IsFalse(sql.Contains("ORDER BY messageid ASC", StringComparison.OrdinalIgnoreCase));
        AssertNoOutOfScopeMessageAccess(sql);
    }

    [TestMethod]
    public void GetFolderMessagesSql_ReadsOnlyDeliveredMessagesForSelectedFolderInUidOrder()
    {
        var sql = SqlServerMessageAdministrationStore.GetFolderMessagesSql;

        AssertMessageProjection(sql);
        StringAssert.Contains(sql, "FROM hm_messages");
        StringAssert.Contains(sql, "messagefolderid = @FolderID");
        StringAssert.Contains(sql, "messagetype = 2");
        StringAssert.Contains(sql, "ORDER BY messageuid ASC, messageid ASC");
        AssertNoOutOfScopeMessageAccess(sql);
    }

    [TestMethod]
    public void GetAccountAddressSql_ReadsOnlyAccountAddressForMessageContentPathResolution()
    {
        var sql = SqlServerMessageAdministrationContentSource.GetAccountAddressSql;

        StringAssert.Contains(sql, "SELECT accountaddress");
        StringAssert.Contains(sql, "FROM hm_accounts");
        StringAssert.Contains(sql, "WHERE accountid = @AccountID");
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_messages", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("messagefilename", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("filecontent", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("smtp", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertMessageProjection(string sql)
    {
        var projectedColumns = new[]
        {
            "messageid,",
            "messageaccountid,",
            "messagefolderid,",
            "messagefilename,",
            "messagetype,",
            "messagefrom,",
            "messagesize,",
            "messagecurnooftries,",
            "messageflags,",
            "messagecreatetime,",
            "messageuid"
        };
        var previousIndex = -1;

        foreach (var column in projectedColumns)
        {
            var index = sql.IndexOf(column, StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(index > previousIndex, $"Expected `{column}` after the previous projected column.");
            previousIndex = index;
        }
    }

    private static void AssertNoOutOfScopeMessageAccess(string sql)
    {
        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_message_metadata", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_messagerecipients", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("smtp", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("filecontent", StringComparison.OrdinalIgnoreCase));
    }
    [TestMethod]
    public void InsertMessageSql_UsesLegacyMessageTableColumnsAndIdentityOutput()
    {
        var sql = SqlServerMessageAdministrationStore.InsertMessageSql;
        StringAssert.Contains(sql, "INSERT INTO hm_messages");
        foreach (var column in new[]
        {
            "messageaccountid",
            "messagefolderid",
            "messagefilename",
            "messagetype",
            "messagefrom",
            "messagesize",
            "messagecurnooftries",
            "messagenexttrytime",
            "messageflags",
            "messagecreatetime",
            "messagelocked",
            "messageuid"
        })
        {
            StringAssert.Contains(sql, column);
        }

        StringAssert.Contains(sql, "OUTPUT INSERTED.messageid");
        StringAssert.Contains(sql, "@AccountID");
        StringAssert.Contains(sql, "@FolderID");
        StringAssert.Contains(sql, "@FileName");
        StringAssert.Contains(sql, "@Uid");
        Assert.IsFalse(sql.Contains("WHERE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
    }
}