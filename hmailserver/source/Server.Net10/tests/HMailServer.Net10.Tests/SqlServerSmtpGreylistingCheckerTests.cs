using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerSmtpGreylistingCheckerTests
{
    [TestMethod]
    public void Sql_UsesLegacyTripletAndWhiteAddressTables()
    {
        StringAssert.Contains(
            SqlServerSmtpGreylistingChecker.SelectWhiteAddressSql,
            "hm_greylisting_whiteaddresses");
        StringAssert.Contains(
            SqlServerSmtpGreylistingChecker.SelectWhiteAddressSql,
            "LIKE whiteipaddress ESCAPE");
        StringAssert.Contains(
            SqlServerSmtpGreylistingChecker.SelectTripletSql,
            "hm_greylisting_triplets WITH (UPDLOCK, HOLDLOCK)");
        StringAssert.Contains(
            SqlServerSmtpGreylistingChecker.SelectTripletSql,
            "glipaddress1 = @IpAddress1");
        StringAssert.Contains(
            SqlServerSmtpGreylistingChecker.SelectTripletSql,
            "glsenderaddress = @SenderAddress");
        StringAssert.Contains(
            SqlServerSmtpGreylistingChecker.InsertTripletSql,
            "glblockedcount");
        StringAssert.Contains(
            SqlServerSmtpGreylistingChecker.MarkTripletBlockedSql,
            "glblockedcount = glblockedcount + 1");
        StringAssert.Contains(
            SqlServerSmtpGreylistingChecker.MarkTripletPassedSql,
            "glpassedcount = glpassedcount + 1");
    }

    [TestMethod]
    public async Task CheckAsync_SkipsWhenDisabledWithoutOpeningSqlConnection()
    {
        var checker = new SqlServerSmtpGreylistingChecker(
            new SqlServerConnectionFactory("Server=invalid;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new SmtpGreylistingOptions { Enabled = false });

        var result = await checker.CheckAsync(
            CreateRequest(),
            CancellationToken.None);

        Assert.IsFalse(result.Deferred);
    }

    [TestMethod]
    public async Task CheckAsync_SkipsAuthenticatedClientsByDefaultWithoutOpeningSqlConnection()
    {
        var checker = new SqlServerSmtpGreylistingChecker(
            new SqlServerConnectionFactory("Server=invalid;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new SmtpGreylistingOptions { Enabled = true });

        var result = await checker.CheckAsync(
            CreateRequest() with { IsAuthenticated = true },
            CancellationToken.None);

        Assert.IsFalse(result.Deferred);
    }

    [TestMethod]
    public async Task CheckAsync_SkipsInvalidClientIpWithoutOpeningSqlConnection()
    {
        var checker = new SqlServerSmtpGreylistingChecker(
            new SqlServerConnectionFactory("Server=invalid;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new SmtpGreylistingOptions { Enabled = true, SkipAuthenticated = false });

        var result = await checker.CheckAsync(
            CreateRequest() with { ClientIPAddress = "not-an-ip" },
            CancellationToken.None);

        Assert.IsFalse(result.Deferred);
    }

    private static SmtpReceiveRequest CreateRequest() =>
        new(
            HeloHost: "client.example",
            IsExtendedSmtp: true,
            MailFrom: "sender@example.test",
            Recipients:
            [
                new SmtpResolvedRecipient(
                    "recipient@example.test",
                    "recipient@example.test",
                    LocalAccountId: 0,
                    IsLocal: false)
            ],
            DeclaredSize: null,
            MessageData: "Subject: Test\r\n\r\nBody\r\n"u8.ToArray(),
            ReceivedUtc: DateTimeOffset.UtcNow,
            ClientIPAddress: "192.0.2.5");
}
