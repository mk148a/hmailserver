using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerDomainAliasAdministrationStoreTests
{
    [TestMethod]
    public void GetDomainAliasesSql_UsesLegacyDomainAliasTableDomainFilterAndIdOrdering()
    {
        var sql = SqlServerDomainAliasAdministrationStore.GetDomainAliasesSql;

        StringAssert.Contains(sql, "daid");
        StringAssert.Contains(sql, "dadomainid");
        StringAssert.Contains(sql, "daalias");
        StringAssert.Contains(sql, "FROM hm_domain_aliases");
        StringAssert.Contains(sql, "WHERE dadomainid = @DomainID");
        StringAssert.Contains(sql, "ORDER BY daid ASC");
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InsertDomainAliasSql_UsesOwnerAndAliasNameAndGeneratedIdentity()
    {
        var sql = SqlServerDomainAliasAdministrationStore.InsertDomainAliasSql;

        StringAssert.Contains(sql, "INSERT INTO hm_domain_aliases");
        StringAssert.Contains(sql, "dadomainid");
        StringAssert.Contains(sql, "daalias");
        StringAssert.Contains(sql, "OUTPUT INSERTED.daid");
        StringAssert.Contains(sql, "@DomainID");
        StringAssert.Contains(sql, "@AliasName");
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateDomainAliasSql_UsesOwnerAndAliasPredicates()
    {
        var sql = SqlServerDomainAliasAdministrationStore.UpdateDomainAliasSql;

        StringAssert.Contains(sql, "UPDATE hm_domain_aliases");
        StringAssert.Contains(sql, "dadomainid = @DomainID");
        StringAssert.Contains(sql, "daalias = @AliasName");
        StringAssert.Contains(sql, "WHERE dadomainid = @OwningDomainID");
        StringAssert.Contains(sql, "AND daid = @AliasID");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteDomainAliasSql_UsesOwnerAndAliasPredicates()
    {
        var sql = SqlServerDomainAliasAdministrationStore.DeleteDomainAliasSql;

        StringAssert.Contains(sql, "DELETE FROM hm_domain_aliases");
        StringAssert.Contains(sql, "WHERE dadomainid = @OwningDomainID");
        StringAssert.Contains(sql, "AND daid = @AliasID");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
    }
}
