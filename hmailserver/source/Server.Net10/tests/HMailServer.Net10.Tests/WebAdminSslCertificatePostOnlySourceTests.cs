using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminSslCertificatePostOnlySourceTests
{
    [TestMethod]
    public void SslCertificateHandlerUsesPostBodyAndRequiresPostCsrfBeforeMutation()
    {
        var handler = ReadWebAdminSource("background_sslcertificate_save.php");
        var authorizationPosition = handler.IndexOf("hmailGetAdminLevel() != ADMIN_SERVER", StringComparison.Ordinal);
        var csrfPosition = handler.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstInputPosition = handler.IndexOf("hmailGetPostVar(", StringComparison.Ordinal);
        var settingsPosition = handler.IndexOf("$obBaseApp->Settings->SSLCertificates", StringComparison.Ordinal);

        Assert.IsTrue(authorizationPosition >= 0, "The SSL certificate handler must retain the server-admin boundary.");
        Assert.IsTrue(csrfPosition > authorizationPosition, "CSRF validation must follow authorization.");
        Assert.IsTrue(firstInputPosition > csrfPosition, "Mutation inputs must be read after CSRF validation.");
        Assert.IsTrue(settingsPosition > csrfPosition, "SSL certificate settings access must follow CSRF validation.");
        Assert.IsFalse(handler.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(handler.Contains("hmailRequirePost();", StringComparison.Ordinal));

        foreach (var field in new[] { "action", "id", "Name", "CertificateFile", "PrivateKeyFile" })
            StringAssert.Contains(handler, $"hmailGetPostVar(\"{field}\"");

        StringAssert.Contains(handler, "$sslCertificates->ItemByDBID($id)");
        StringAssert.Contains(handler, "$sslCertificates->Add();");
        StringAssert.Contains(handler, "$sslCertificates->DeleteByDBID($id);");
        StringAssert.Contains(handler, "$sslCertificate->Name = $Name;");
        StringAssert.Contains(handler, "$sslCertificate->CertificateFile = $CertificateFile;");
        StringAssert.Contains(handler, "$sslCertificate->PrivateKeyFile = $PrivateKeyFile;");
        StringAssert.Contains(handler, "$sslCertificate->Save();");

        var editForm = ReadWebAdminSource("hm_sslcertificate.php");
        StringAssert.Contains(editForm, "method=\"post\"");
        StringAssert.Contains(editForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(editForm, "PrintHidden(\"page\", \"background_sslcertificate_save\")");
        StringAssert.Contains(editForm, "PrintHidden(\"action\", \"$action\")");
        StringAssert.Contains(editForm, "PrintHidden(\"id\", \"$id\")");
        foreach (var field in new[] { "Name", "CertificateFile", "PrivateKeyFile" })
            StringAssert.Contains(editForm, $"PrintPropertyEditRow(\"{field}\"");

        var deleteForm = ReadWebAdminSource("hm_sslcertificates.php");
        StringAssert.Contains(deleteForm, "method=\\\"post\\\"");
        StringAssert.Contains(deleteForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(deleteForm, "PrintHidden(\"page\", \"background_sslcertificate_save\")");
        StringAssert.Contains(deleteForm, "PrintHidden(\"action\", \"delete\")");
        StringAssert.Contains(deleteForm, "PrintHidden(\"id\", $id)");
    }

    private static string ReadWebAdminSource(string fileName)
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
                fileName);

            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail($"Could not locate hmailserver/source/WebAdmin/{fileName} from the test output directory.");
        return string.Empty;
    }
}
