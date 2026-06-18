using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerAutoBanLogonFailureRecorderTests
{
    [TestMethod]
    public void Sql_UsesLegacyAutoBanSettingsAndFailureTables()
    {
        StringAssert.Contains(
            SqlServerAutoBanLogonFailureRecorder.SelectAutoBanSettingsSql,
            "AutoBanOnLogonFailureEnabled");
        StringAssert.Contains(
            SqlServerAutoBanLogonFailureRecorder.SelectAutoBanSettingsSql,
            "MaxInvalidLogonAttempts");
        StringAssert.Contains(
            SqlServerAutoBanLogonFailureRecorder.SelectAutoBanSettingsSql,
            "LogonAttemptsWithinMinutes");
        StringAssert.Contains(
            SqlServerAutoBanLogonFailureRecorder.SelectAutoBanSettingsSql,
            "AutoBanMinutes");

        StringAssert.Contains(SqlServerAutoBanLogonFailureRecorder.CountFailuresSql, "hm_logon_failures");
        StringAssert.Contains(SqlServerAutoBanLogonFailureRecorder.CountFailuresSql, "ipaddress1 = @IpAddress1");
        StringAssert.Contains(SqlServerAutoBanLogonFailureRecorder.CountFailuresSql, "ipaddress2 IS NULL");
        StringAssert.Contains(SqlServerAutoBanLogonFailureRecorder.InsertFailureSql, "SYSUTCDATETIME()");
        StringAssert.Contains(SqlServerAutoBanLogonFailureRecorder.ClearOldFailuresSql, "DATEADD(minute, -@LogonAttemptsWithinMinutes");
    }

    [TestMethod]
    public void InsertAutoBanRangeSql_CreatesDenyRangeWithLegacyShape()
    {
        var sql = SqlServerAutoBanLogonFailureRecorder.InsertAutoBanRangeSql;

        StringAssert.Contains(sql, "INSERT INTO hm_securityranges");
        StringAssert.Contains(sql, "rangepriorityid");
        StringAssert.Contains(sql, "100");
        StringAssert.Contains(sql, "rangelowerip1");
        StringAssert.Contains(sql, "rangeupperip1");
        StringAssert.Contains(sql, "rangeoptions");
        StringAssert.Contains(sql, "0");
        StringAssert.Contains(sql, "rangename");
        StringAssert.Contains(sql, "rangeexpires");
        StringAssert.Contains(sql, "DATEADD(minute, @AutoBanMinutes");
    }

    [TestMethod]
    public void BuildRangeNameCandidate_UsesLegacyAutoBanPrefixAndTruncates()
    {
        Assert.AreEqual(
            "Auto-ban: user@example.test",
            SqlServerAutoBanLogonFailureRecorder.BuildRangeNameCandidate("user@example.test", 0));
        Assert.AreEqual(
            "Auto-ban: user@example.test (3)",
            SqlServerAutoBanLogonFailureRecorder.BuildRangeNameCandidate("user@example.test", 3));

        var longName = SqlServerAutoBanLogonFailureRecorder.BuildRangeNameCandidate(new string('x', 200), 0);
        Assert.AreEqual(100, longName.Length);
        StringAssert.StartsWith(longName, "Auto-ban: ");
    }
}
