using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerSurblServerAdministrationStoreTests
{
    [TestMethod]
    public void GetSurblServersSql_UsesLegacyColumnsAndDatabaseIdOrdering()
    {
        var sql = SqlServerSurblServerAdministrationStore.GetSurblServersSql;

        StringAssert.Contains(sql, "FROM hm_surblservers");
        StringAssert.Contains(sql, "surblid");
        StringAssert.Contains(sql, "surblactive");
        StringAssert.Contains(sql, "surblhost");
        StringAssert.Contains(sql, "surblrejectmessage");
        StringAssert.Contains(sql, "surblscore");
        StringAssert.Contains(sql, "ORDER BY surblid ASC");
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void GetSurblServersSql_RemainsReadOnlyAndDoesNotTouchDnsOrSmtpRuntime()
    {
        var sql = SqlServerSurblServerAdministrationStore.GetSurblServersSql;

        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_settings", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_dnsbl", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("xp_", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("OPENROWSET", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InsertSurblServerSql_UsesLegacyColumnsAndGeneratedIdentity()
    {
        var sql = SqlServerSurblServerAdministrationStore.InsertSurblServerSql;

        StringAssert.Contains(sql, "INSERT INTO hm_surblservers");
        StringAssert.Contains(sql, "surblactive");
        StringAssert.Contains(sql, "surblhost");
        StringAssert.Contains(sql, "surblrejectmessage");
        StringAssert.Contains(sql, "surblscore");
        StringAssert.Contains(sql, "OUTPUT INSERTED.surblid");
        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_dnsbl", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateSurblServerSql_UsesLegacyFieldsAndIdentityPredicate()
    {
        var sql = SqlServerSurblServerAdministrationStore.UpdateSurblServerSql;

        StringAssert.Contains(sql, "UPDATE hm_surblservers");
        StringAssert.Contains(sql, "surblactive = @active");
        StringAssert.Contains(sql, "surblhost = @dnsHost");
        StringAssert.Contains(sql, "surblrejectmessage = @rejectMessage");
        StringAssert.Contains(sql, "surblscore = @score");
        StringAssert.Contains(sql, "WHERE surblid = @id");
        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_dnsbl", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteSurblServerSql_UsesParameterizedIdentityPredicate()
    {
        var sql = SqlServerSurblServerAdministrationStore.DeleteSurblServerSql;

        StringAssert.Contains(sql, "DELETE FROM hm_surblservers");
        StringAssert.Contains(sql, "WHERE surblid = @id");
        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_dnsbl", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteSurblServerByIdAsync_UsesBooleanIntIdStoreContract()
    {
        var method = typeof(SqlServerSurblServerAdministrationStore).GetMethod(
            nameof(SqlServerSurblServerAdministrationStore.DeleteSurblServerByIdAsync));

        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(ValueTask<bool>), method.ReturnType);
        CollectionAssert.AreEqual(
            new[] { typeof(int), typeof(CancellationToken) },
            method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }
}
