using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminTcpIpPortPostOnlySourceTests
{
    [TestMethod]
    public void TcpIpPortHandlerUsesPostBodyAndRequiresPostCsrfBeforeReads()
    {
        var handler = ReadWebAdminFile("background_tcpipport_save.php");
        var authorizationPosition = handler.IndexOf("hmailGetAdminLevel() != 2", StringComparison.Ordinal);
        var csrfPosition = handler.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstInputPosition = handler.IndexOf("hmailGetPostVar(", StringComparison.Ordinal);

        Assert.IsTrue(authorizationPosition >= 0, "The TCP/IP port handler must retain the server-admin boundary.");
        Assert.IsTrue(csrfPosition > authorizationPosition, "CSRF validation must follow authorization.");
        Assert.IsTrue(firstInputPosition > csrfPosition, "Mutation inputs must be read after CSRF validation.");
        Assert.IsFalse(handler.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(handler.Contains("hmailRequirePost();", StringComparison.Ordinal));

        foreach (var field in new[]
        {
            "tcpipportid",
            "protocol",
            "portnumber",
            "action",
            "ConnectionSecurity",
            "SSLCertificateID",
            "Address"
        })
        {
            StringAssert.Contains(handler, $"hmailGetPostVar(\"{field}\"");
        }

        StringAssert.Contains(handler, "$obBaseApp->Settings()");
        StringAssert.Contains(handler, "$obSettings->TCPIPPorts");
        StringAssert.Contains(handler, "$obTCPIPPorts->ItemByDBID($tcpipportid)");
        StringAssert.Contains(handler, "$obTCPIPPorts->Add()");
        StringAssert.Contains(handler, "$obTCPIPPorts->DeleteByDBID($tcpipportid)");

        foreach (var property in new[]
        {
            "Protocol",
            "PortNumber",
            "ConnectionSecurity",
            "SSLCertificateID",
            "Address"
        })
        {
            StringAssert.Contains(handler, $"$obTCPIPPort->{property} =");
        }

        StringAssert.Contains(handler, "$obTCPIPPort->Save();");
        StringAssert.Contains(handler, "$obBaseApp->Stop();");
        StringAssert.Contains(handler, "$obBaseApp->Start();");
        StringAssert.Contains(handler, "$tcpipportid = $obTCPIPPort->ID;");
        StringAssert.Contains(handler, "header(\"Location: index.php?page=tcpipports\")");
        StringAssert.Contains(handler, "header(\"Location: index.php?page=tcpipport&action=edit&tcpipportid=$tcpipportid\")");
    }

    [TestMethod]
    public void TcpIpPortFormsUsePostCsrfAndExpectedFields()
    {
        var editForm = ReadWebAdminFile("hm_tcpipport.php");
        StringAssert.Contains(editForm, "method=\"post\"");
        StringAssert.Contains(editForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(editForm, "PrintHidden(\"page\", \"background_tcpipport_save\")");
        StringAssert.Contains(editForm, "PrintHidden(\"action\", \"$action\")");
        StringAssert.Contains(editForm, "PrintHidden(\"tcpipportid\", \"$tcpipportid\")");

        foreach (var field in new[] { "protocol", "portnumber", "ConnectionSecurity", "SSLCertificateID" })
            StringAssert.Contains(editForm, $"name=\"{field}\"");

        StringAssert.Contains(editForm, "PrintPropertyEditRow(\"Address\"");

        var deleteForm = ReadWebAdminFile("hm_tcpipports.php");
        StringAssert.Contains(deleteForm, "method=\\\"post\\\"");
        StringAssert.Contains(deleteForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(deleteForm, "PrintHidden(\"page\", \"background_tcpipport_save\")");
        StringAssert.Contains(deleteForm, "PrintHidden(\"action\", \"delete\")");
        StringAssert.Contains(deleteForm, "PrintHidden(\"tcpipportid\", $portid)");
    }

    private static string ReadWebAdminFile(string fileName)
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
