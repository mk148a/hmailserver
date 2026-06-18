using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerExternalFetchAccountStoreTests
{
    [TestMethod]
    public void LeaseReadyAccountsSql_LeasesDueActiveLegacyFetchAccounts()
    {
        var sql = SqlServerExternalFetchAccountStore.LeaseReadyAccountsSql;

        StringAssert.Contains(sql, "FROM hm_fetchaccounts AS fa WITH (UPDLOCK, READPAST, ROWLOCK)");
        StringAssert.Contains(sql, "faactive <> 0");
        StringAssert.Contains(sql, "falocked = 0");
        StringAssert.Contains(sql, "fanexttry <= SYSUTCDATETIME()");
        StringAssert.Contains(sql, "FROM hm_accounts AS a");
        StringAssert.Contains(sql, "accountactive <> 0");
        StringAssert.Contains(sql, "INNER JOIN hm_domains AS d");
        StringAssert.Contains(sql, "domainactive <> 0");
        StringAssert.Contains(sql, "UPDATE fa");
        StringAssert.Contains(sql, "SET fa.falocked = 1");
        StringAssert.Contains(sql, "OUTPUT");
        StringAssert.Contains(sql, "inserted.faid");
        StringAssert.Contains(sql, "inserted.faconnectionsecurity");
        StringAssert.Contains(sql, "a.accountaddress");
    }

    [TestMethod]
    public void CompletionSql_ReleasesLockAndSchedulesNextTryWithLegacyMinutes()
    {
        StringAssert.Contains(SqlServerExternalFetchAccountStore.CompleteSql, "SET");
        StringAssert.Contains(SqlServerExternalFetchAccountStore.CompleteSql, "falocked = 0");
        StringAssert.Contains(
            SqlServerExternalFetchAccountStore.CompleteSql,
            "fanexttry = DATEADD(minute, faminutes, SYSUTCDATETIME())");
        StringAssert.Contains(SqlServerExternalFetchAccountStore.CompleteSql, "WHERE faid = @FetchAccountId");
        StringAssert.Contains(SqlServerExternalFetchAccountStore.CompleteSql, "AND falocked = 1");

        StringAssert.Contains(SqlServerExternalFetchAccountStore.ReleaseSql, "SET falocked = 0");
        StringAssert.Contains(SqlServerExternalFetchAccountStore.ResetLocksSql, "WHERE falocked <> 0");
        StringAssert.Contains(SqlServerExternalFetchAccountStore.DeferInactiveAccountsSql, "NOT EXISTS");
        StringAssert.Contains(SqlServerExternalFetchAccountStore.DeferInactiveAccountsSql, "DATEADD(minute, fa.faminutes");
    }

    [TestMethod]
    public void UidSql_UsesLegacyUidTrackingTable()
    {
        StringAssert.Contains(SqlServerExternalFetchAccountStore.SelectKnownUidsSql, "FROM hm_fetchaccounts_uids");
        StringAssert.Contains(SqlServerExternalFetchAccountStore.SelectKnownUidsSql, "uidfaid = @FetchAccountId");
        StringAssert.Contains(SqlServerExternalFetchAccountStore.InsertKnownUidSql, "INSERT INTO hm_fetchaccounts_uids");
        StringAssert.Contains(SqlServerExternalFetchAccountStore.InsertKnownUidSql, "SYSUTCDATETIME()");
        StringAssert.Contains(SqlServerExternalFetchAccountStore.DeleteKnownUidSql, "WHERE uidid = @UidId");
    }

    [TestMethod]
    public void EnumValues_MatchLegacyDatabaseValues()
    {
        var serverTypes = Enum.GetValues<ExternalFetchServerType>()
            .ToDictionary(static value => value.ToString(), GetEnumValue);
        var securityModes = Enum.GetValues<ExternalFetchConnectionSecurity>()
            .ToDictionary(static value => value.ToString(), GetEnumValue);

        Assert.AreEqual(0, serverTypes[nameof(ExternalFetchServerType.Pop3)]);
        Assert.AreEqual(0, securityModes[nameof(ExternalFetchConnectionSecurity.None)]);
        Assert.AreEqual(1, securityModes[nameof(ExternalFetchConnectionSecurity.Ssl)]);
        Assert.AreEqual(2, securityModes[nameof(ExternalFetchConnectionSecurity.StartTlsOptional)]);
        Assert.AreEqual(3, securityModes[nameof(ExternalFetchConnectionSecurity.StartTlsRequired)]);
    }

    private static int GetEnumValue<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        Convert.ToInt32(value);
}
