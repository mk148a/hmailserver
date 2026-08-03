using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerFetchAccountAdministrationStoreTests
{
    [TestMethod]
    public void GetFetchAccountsSql_UsesLegacyFetchAccountTableAccountFilterAndIdOrdering()
    {
        var sql = SqlServerFetchAccountAdministrationStore.GetFetchAccountsSql;

        StringAssert.Contains(sql, "faid");
        StringAssert.Contains(sql, "faaccountid");
        StringAssert.Contains(sql, "faaccountname");
        StringAssert.Contains(sql, "faserveraddress");
        StringAssert.Contains(sql, "faserverport");
        StringAssert.Contains(sql, "faservertype");
        StringAssert.Contains(sql, "fausername");
        StringAssert.Contains(sql, "faminutes");
        StringAssert.Contains(sql, "fadaystokeep");
        StringAssert.Contains(sql, "faactive");
        StringAssert.Contains(sql, "faprocessmimerecipients");
        StringAssert.Contains(sql, "faprocessmimedate");
        StringAssert.Contains(sql, "faconnectionsecurity");
        StringAssert.Contains(sql, "fauseantispam");
        StringAssert.Contains(sql, "fauseantivirus");
        StringAssert.Contains(sql, "faenablerouterecipients");
        StringAssert.Contains(sql, "famimerecipientheaders");
        StringAssert.Contains(sql, "fanexttry");
        StringAssert.Contains(sql, "falocked");
        StringAssert.Contains(sql, "FROM hm_fetchaccounts");
        StringAssert.Contains(sql, "WHERE faaccountid = @AccountID");
        StringAssert.Contains(sql, "ORDER BY faid ASC");
        Assert.IsFalse(sql.Contains("fapassword", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void GetFetchAccountsSql_ProjectsOnlyReadOnlyAdministrationColumnsInReaderOrder()
    {
        var sql = SqlServerFetchAccountAdministrationStore.GetFetchAccountsSql;

        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_fetchaccounts_uids", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("fapassword", StringComparison.OrdinalIgnoreCase));

        var projectedColumns = new[]
        {
            "faid,",
            "faaccountid,",
            "faaccountname,",
            "faserveraddress,",
            "faserverport,",
            "faservertype,",
            "fausername,",
            "faminutes,",
            "fadaystokeep,",
            "faactive,",
            "faprocessmimerecipients,",
            "faprocessmimedate,",
            "faconnectionsecurity,",
            "fauseantispam,",
            "fauseantivirus,",
            "faenablerouterecipients,",
            "famimerecipientheaders,",
            "CONVERT(varchar(19), fanexttry, 120) AS fanexttry,",
            "falocked"
        };
        var previousIndex = -1;

        foreach (var column in projectedColumns)
        {
            var index = sql.IndexOf(column, StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(index > previousIndex, $"Expected `{column}` after the previous projected column.");
            previousIndex = index;
        }
    }

    [TestMethod]
    public void SetRetryNowSql_UsesParentAndFetchAccountOwnershipFiltersAndGetDate()
    {
        var sql = SqlServerFetchAccountAdministrationStore.SetRetryNowSql;

        StringAssert.Contains(sql, "UPDATE hm_fetchaccounts");
        StringAssert.Contains(sql, "SET fanexttry = GETDATE()");
        StringAssert.Contains(sql, "WHERE faid = @FetchAccountID");
        StringAssert.Contains(sql, "AND faaccountid = @AccountID");
        Assert.IsFalse(sql.Contains("@FAID", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteFetchAccountSql_UsesSelectedFetchAccountAndOwningAccountFilters()
    {
        var sql = SqlServerFetchAccountAdministrationStore.DeleteFetchAccountSql;

        StringAssert.Contains(sql, "DELETE FROM hm_fetchaccounts");
        StringAssert.Contains(sql, "WHERE faid = @FetchAccountID");
        StringAssert.Contains(sql, "AND faaccountid = @AccountID");
        Assert.IsFalse(sql.Contains("@FAID", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteFetchAccountUidsSql_UsesSelectedFetchAccountFilter()
    {
        var sql = SqlServerFetchAccountAdministrationStore.DeleteFetchAccountUidsSql;

        StringAssert.Contains(sql, "DELETE FROM hm_fetchaccounts_uids");
        StringAssert.Contains(sql, "WHERE uidfaid = @FetchAccountID");
        Assert.IsFalse(sql.Contains("@AccountID", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("@UIDFAID", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InsertFetchAccountSql_PreservesLegacyColumnsAndGeneratedId()
    {
        var sql = SqlServerFetchAccountAdministrationStore.InsertFetchAccountSql;

        StringAssert.Contains(sql, "INSERT INTO hm_fetchaccounts");
        StringAssert.Contains(sql, "OUTPUT INSERTED.faid");
        StringAssert.Contains(sql, "fapassword");
        StringAssert.Contains(sql, "fanexttry");
        StringAssert.Contains(sql, "falocked");
        StringAssert.Contains(sql, "@AccountID");
        StringAssert.Contains(sql, "@MimeRecipientHeaders");
        Assert.IsFalse(sql.Contains("+ account", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void BackupFetchAccountsSql_ProjectsCiphertextAndScopesByAccountInFaidOrder()
    {
        var sql = SqlServerBackupFetchAccountAdministrationStore.GetBackupFetchAccountsSql;

        StringAssert.Contains(sql, "fapassword");
        StringAssert.Contains(sql, "FROM hm_fetchaccounts");
        StringAssert.Contains(sql, "WHERE faaccountid = @AccountID");
        StringAssert.Contains(sql, "ORDER BY faid ASC");
        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_fetchaccounts_uids", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(
            sql.IndexOf("fausername", StringComparison.OrdinalIgnoreCase)
                < sql.IndexOf("fapassword", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void BackupFetchAccountUidsSql_UsesUidFilterAndPreservesReaderOrder()
    {
        var sql = SqlServerBackupFetchAccountAdministrationStore.GetBackupFetchAccountUidsSql;

        StringAssert.Contains(sql, "uidvalue");
        StringAssert.Contains(sql, "CONVERT(varchar(19), uidtime, 120) AS uidtime");
        StringAssert.Contains(sql, "FROM hm_fetchaccounts_uids");
        StringAssert.Contains(sql, "WHERE uidfaid = @FetchAccountID");
        Assert.IsFalse(sql.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase));
    }
}
