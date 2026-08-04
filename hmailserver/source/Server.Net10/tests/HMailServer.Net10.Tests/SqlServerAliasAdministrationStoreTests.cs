using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerAliasAdministrationStoreTests
{
    [TestMethod]
    public void GetAliasesSql_UsesLegacyAliasTableDomainFilterAndNameOrdering()
    {
        var sql = SqlServerAliasAdministrationStore.GetAliasesSql;

        StringAssert.Contains(sql, "aliasid");
        StringAssert.Contains(sql, "aliasdomainid");
        StringAssert.Contains(sql, "aliasname");
        StringAssert.Contains(sql, "aliasvalue");
        StringAssert.Contains(sql, "aliasactive");
        StringAssert.Contains(sql, "FROM hm_aliases");
        StringAssert.Contains(sql, "WHERE aliasdomainid = @DomainID");
        StringAssert.Contains(sql, "ORDER BY aliasname ASC");
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InsertAliasSql_UsesOwnerScopedFieldsAndGeneratedIdentity()
    {
        var sql = SqlServerAliasAdministrationStore.InsertAliasSql;

        StringAssert.Contains(sql, "INSERT INTO hm_aliases");
        StringAssert.Contains(sql, "aliasdomainid, aliasname, aliasvalue, aliasactive");
        StringAssert.Contains(sql, "OUTPUT INSERTED.aliasid");
        StringAssert.Contains(sql, "@DomainID");
        StringAssert.Contains(sql, "@Name");
        StringAssert.Contains(sql, "@Value");
        StringAssert.Contains(sql, "@Active");
        Assert.IsFalse(sql.Contains("MAX(", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("IDENT_CURRENT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateAliasSql_UsesOwnerAndAliasPredicatesAndAllMutableFields()
    {
        var sql = SqlServerAliasAdministrationStore.UpdateAliasSql;

        StringAssert.Contains(sql, "UPDATE hm_aliases");
        StringAssert.Contains(sql, "aliasdomainid = @DomainID");
        StringAssert.Contains(sql, "aliasname = @Name");
        StringAssert.Contains(sql, "aliasvalue = @Value");
        StringAssert.Contains(sql, "aliasactive = @Active");
        StringAssert.Contains(sql, "WHERE aliasdomainid = @OwningDomainID");
        StringAssert.Contains(sql, "AND aliasid = @AliasID");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteAliasSql_UsesOwnerAndAliasPredicates()
    {
        var sql = SqlServerAliasAdministrationStore.DeleteAliasSql;

        StringAssert.Contains(sql, "DELETE FROM hm_aliases");
        StringAssert.Contains(sql, "WHERE aliasdomainid = @OwningDomainID");
        StringAssert.Contains(sql, "AND aliasid = @AliasID");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
    }
}
