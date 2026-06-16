using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImapRecentFlagStoreTests
{
    [TestMethod]
    public void RecentFlagSql_SelectsAndClearsMailboxRecentFlags()
    {
        StringAssert.Contains(SqlServerImapRecentFlagStore.SelectRecentUidsSql, "SELECT messageuid");
        StringAssert.Contains(SqlServerImapRecentFlagStore.SelectRecentUidsSql, "(messageflags & @RecentFlag) = @RecentFlag");
        StringAssert.Contains(SqlServerImapRecentFlagStore.ClearRecentFlagsSql, "SET messageflags = messageflags & ~ @RecentFlag");
        StringAssert.Contains(SqlServerImapRecentFlagStore.ClearRecentFlagsSql, "messagetype = 2");
    }
}
