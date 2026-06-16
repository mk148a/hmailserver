using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerSmtpRecipientValidatorTests
{
    [TestMethod]
    public void ValidatorSql_UsesLocalDomainAccountAliasDistributionListAndDomainAliasTables()
    {
        StringAssert.Contains(SqlServerSmtpRecipientValidator.SelectDomainSql, "FROM hm_domains AS d");
        StringAssert.Contains(SqlServerSmtpRecipientValidator.SelectDomainSql, "domainuseplusaddressing");
        StringAssert.Contains(SqlServerSmtpRecipientValidator.SelectDomainByAliasSql, "FROM hm_domain_aliases AS da");
        StringAssert.Contains(SqlServerSmtpRecipientValidator.SelectDomainByAliasSql, "ON d.domainid = da.dadomainid");
        StringAssert.Contains(SqlServerSmtpRecipientValidator.SelectAccountSql, "FROM hm_accounts");
        StringAssert.Contains(SqlServerSmtpRecipientValidator.SelectAccountSql, "accountactive");
        StringAssert.Contains(SqlServerSmtpRecipientValidator.SelectAliasSql, "FROM hm_aliases");
        StringAssert.Contains(SqlServerSmtpRecipientValidator.SelectAliasSql, "aliasactive");
        StringAssert.Contains(SqlServerSmtpRecipientValidator.SelectDistributionListSql, "FROM hm_distributionlists");
        StringAssert.Contains(SqlServerSmtpRecipientValidator.SelectDistributionListMembersSql, "FROM hm_distributionlistsrecipients");
        StringAssert.Contains(SqlServerSmtpRecipientValidator.SelectRoutesSql, "FROM hm_routes");
        StringAssert.Contains(SqlServerSmtpRecipientValidator.SelectRoutesSql, "routetreatsecurityaslocal");
        StringAssert.Contains(SqlServerSmtpRecipientValidator.SelectRoutesSql, "routeuseauthentication");
        StringAssert.Contains(SqlServerSmtpRecipientValidator.SelectRoutesSql, "routeauthenticationusername");
        StringAssert.Contains(SqlServerSmtpRecipientValidator.SelectRoutesSql, "routeauthenticationpassword");
        StringAssert.Contains(SqlServerSmtpRecipientValidator.SelectRouteAddressSql, "FROM hm_routeaddresses");
        StringAssert.Contains(SqlServerSmtpRecipientValidator.SelectRouteAddressSql, "routeaddressrouteid");
    }
}
