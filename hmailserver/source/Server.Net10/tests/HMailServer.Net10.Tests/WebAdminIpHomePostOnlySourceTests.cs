using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminIpHomePostOnlySourceTests
{
    [TestMethod]
    public void IpHomeHandlerUsesPostBodyAndRequiresPostCsrfAfterServerAdminGuard()
    {
        var source = ReadWebAdminSource("background_iphome_save.php");
        var authorizationPosition = source.IndexOf("hmailGetAdminLevel() != 2", StringComparison.Ordinal);
        var csrfPosition = source.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstPostReadPosition = source.IndexOf("hmailGetPostVar(", StringComparison.Ordinal);

        Assert.IsTrue(authorizationPosition >= 0, "The IP home handler must retain the server-admin boundary.");
        Assert.IsTrue(csrfPosition > authorizationPosition, "CSRF validation must follow authorization.");
        Assert.IsTrue(firstPostReadPosition > csrfPosition, "Mutation inputs must be read after CSRF validation.");
        Assert.IsFalse(source.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("hmailRequirePost();", StringComparison.Ordinal));

        foreach (var field in new[] { "iphomeid", "iphomeaddress", "action" })
            StringAssert.Contains(source, $"hmailGetPostVar(\"{field}\"");

        StringAssert.Contains(source, "$obSettings\t= $obBaseApp->Settings();");
        StringAssert.Contains(source, "$obIPHomes  = $obSettings->IPHomes;");
        StringAssert.Contains(source, "$obIPHome = $obIPHomes->ItemByDBID($iphomeid);");
        StringAssert.Contains(source, "$obIPHome = $obIPHomes->Add();");
        StringAssert.Contains(source, "$obIPHomes->DeleteByDBID($iphomeid);");
        StringAssert.Contains(source, "$obIPHome->IPAddress = $iphomeaddress;");
        StringAssert.Contains(source, "$obIPHome->Save();");
        StringAssert.Contains(source, "header(\"Location: index.php?page=multihoming\");");
        StringAssert.Contains(source, "header(\"Location: index.php?page=iphome&action=edit&iphomeid=$iphomeid\");");
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
