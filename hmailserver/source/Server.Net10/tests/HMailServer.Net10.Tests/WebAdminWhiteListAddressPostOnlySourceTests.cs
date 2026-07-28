using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminWhiteListAddressPostOnlySourceTests
{
    [TestMethod]
    public void WhiteListAddressHandlerUsesPostBodyAndRequiresPostCsrfBeforeMutation()
    {
        var handler = ReadWebAdminSource("background_whitelistaddress_save.php");
        var authorizationPosition = handler.IndexOf("hmailGetAdminLevel() != 2", StringComparison.Ordinal);
        var csrfPosition = handler.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstInputPosition = handler.IndexOf("hmailGetPostVar(", StringComparison.Ordinal);
        var settingsPosition = handler.IndexOf("$obBaseApp->Settings()->AntiSpam()->WhiteListAddresses", StringComparison.Ordinal);

        Assert.IsTrue(authorizationPosition >= 0, "The whitelist handler must retain the server-admin boundary.");
        Assert.IsTrue(csrfPosition > authorizationPosition, "CSRF validation must follow authorization.");
        Assert.IsTrue(firstInputPosition > csrfPosition, "Mutation inputs must be read after CSRF validation.");
        Assert.IsTrue(settingsPosition > csrfPosition, "Whitelist settings access must follow CSRF validation.");
        Assert.IsFalse(handler.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(handler.Contains("hmailRequirePost();", StringComparison.Ordinal));

        foreach (var field in new[]
        {
            "ID",
            "action",
            "LowerIPAddress",
            "UpperIPAddress",
            "EmailAddress",
            "Description"
        })
            StringAssert.Contains(handler, $"hmailGetPostVar(\"{field}\"");

        StringAssert.Contains(handler, "$obWhiteListAddresses->ItemByDBID($ID)");
        StringAssert.Contains(handler, "$obWhiteListAddresses->Add();");
        StringAssert.Contains(handler, "$obWhiteListAddresses->DeleteByDBID($ID);");
        StringAssert.Contains(handler, "$LowerIPAddress = \"0.0.0.0\";");
        StringAssert.Contains(handler, "$UpperIPAddress = \"255.255.255.255\";");
        StringAssert.Contains(handler, "$EmailAddress = \"*\";");
        StringAssert.Contains(handler, "$obAddress->LowerIPAddress  = $LowerIPAddress;");
        StringAssert.Contains(handler, "$obAddress->UpperIPAddress  = $UpperIPAddress;");
        StringAssert.Contains(handler, "$obAddress->EmailAddress    = $EmailAddress;");
        StringAssert.Contains(handler, "$obAddress->Description     = $Description;");
        StringAssert.Contains(handler, "$obAddress->Save();");
        StringAssert.Contains(handler, "header(\"Location: index.php?page=whitelistaddresses\");");

        var editForm = ReadWebAdminSource("hm_whitelistaddress.php");
        StringAssert.Contains(editForm, "method=\"post\"");
        StringAssert.Contains(editForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(editForm, "PrintHidden(\"page\", \"background_whitelistaddress_save\")");
        StringAssert.Contains(editForm, "PrintHidden(\"action\", \"$action\")");
        StringAssert.Contains(editForm, "PrintHidden(\"ID\", \"$ID\")");

        var deleteForm = ReadWebAdminSource("hm_whitelistaddresses.php");
        StringAssert.Contains(deleteForm, "method=\\\"post\\\"");
        StringAssert.Contains(deleteForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(deleteForm, "PrintHidden(\"page\", \"background_whitelistaddress_save\")");
        StringAssert.Contains(deleteForm, "PrintHidden(\"action\", \"delete\")");
        StringAssert.Contains(deleteForm, "PrintHidden(\"ID\", $ID)");
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
