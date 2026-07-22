using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerBackupPreflightAdministrationStoreTests
{
    [TestMethod]
    public void MessagePlacementSql_MatchesLegacyReadOnlyPredicate()
    {
        var sql = SqlServerBackupPreflightAdministrationStore
            .AreAllMessageFilesInDataDirectorySql;

        StringAssert.Contains(sql, "FROM hm_messages");
        StringAssert.Contains(sql, "LEFT(messagefilename, LEN(@DataDirectory)) <> @DataDirectory");
        StringAssert.Contains(sql, "LEFT(messagefilename, 1) <> N'{'");
        StringAssert.Contains(sql, "COUNT_BIG(*) = 0");
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN ", StringComparison.OrdinalIgnoreCase));
    }
}
