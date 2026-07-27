using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminSslTlsPostOnlySourceTests
{
    [TestMethod]
    public void SslTlsSaveUsesPostBodyAndRequiresPostCsrf()
    {
        var source = ReadSslTlsPage();
        var saveStart = source.IndexOf("if($action == \"save\")", StringComparison.Ordinal);
        var renderStart = source.IndexOf("$VerifyRemoteSslCertificate", saveStart, StringComparison.Ordinal);

        Assert.IsTrue(saveStart >= 0, "The SSL/TLS save branch was not found.");
        Assert.IsTrue(renderStart > saveStart, "The SSL/TLS save branch boundary was not found.");

        var saveBranch = source.Substring(saveStart, renderStart - saveStart);
        var csrfPosition = saveBranch.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstMutationPosition = saveBranch.IndexOf("$obSettings->", StringComparison.Ordinal);

        StringAssert.Contains(source, "hmailGetPostVar(\"action\"");
        StringAssert.Contains(source, "method=\"post\"");
        StringAssert.Contains(source, "PrintHiddenCsrfToken();");
        Assert.IsTrue(csrfPosition >= 0, "The SSL/TLS save branch must require a POST CSRF token.");
        Assert.IsTrue(firstMutationPosition > csrfPosition, "TLS settings must be written after CSRF validation.");

        foreach (var name in new[]
        {
            "VerifyRemoteSslCertificate",
            "SslCipherList",
            "TlsVersion10Enabled",
            "TlsVersion11Enabled",
            "TlsVersion12Enabled",
            "TlsVersion13Enabled",
            "TlsOptionPreferServerCiphersEnabled",
            "TlsOptionPrioritizeChaChaEnabled"
        })
        {
            StringAssert.Contains(saveBranch, $"hmailGetPostVar(\"{name}\"");
            Assert.IsFalse(
                saveBranch.Contains($"hmailGetVar(\"{name}\"", StringComparison.Ordinal),
                $"TLS mutation field {name} must not use the mixed GET/POST accessor.");
        }

        StringAssert.Contains(saveBranch, "$obSettings->VerifyRemoteSslCertificate");
        StringAssert.Contains(saveBranch, "$obSettings->SslCipherList");
        StringAssert.Contains(saveBranch, "$obSettings->TlsOptionPrioritizeChaChaEnabled = 0;");
    }

    private static string ReadSslTlsPage()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var path = Path.Combine(
                directory.FullName,
                "hmailserver",
                "source",
                "WebAdmin",
                "hm_ssltls.php");

            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail("Could not locate hmailserver/source/WebAdmin/hm_ssltls.php from the test output directory.");
        return string.Empty;
    }
}
