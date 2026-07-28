using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminDnsBlackListPostOnlySourceTests
{
    [TestMethod]
    public void DnsBlackListHandlerUsesPostBodyAndRequiresPostCsrfBeforeMutation()
    {
        var handler = ReadWebAdminSource("background_dnsblacklist_save.php");
        var authorizationPosition = handler.IndexOf("hmailGetAdminLevel() != ADMIN_SERVER", StringComparison.Ordinal);
        var csrfPosition = handler.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstInputPosition = handler.IndexOf("hmailGetPostVar(", StringComparison.Ordinal);
        var settingsPosition = handler.IndexOf("$obBaseApp->Settings->AntiSpam->DNSBlackLists", StringComparison.Ordinal);

        Assert.IsTrue(authorizationPosition >= 0, "The DNSBL handler must retain the server-admin boundary.");
        Assert.IsTrue(csrfPosition > authorizationPosition, "CSRF validation must follow authorization.");
        Assert.IsTrue(firstInputPosition > csrfPosition, "Mutation inputs must be read after CSRF validation.");
        Assert.IsTrue(settingsPosition > csrfPosition, "DNSBL settings access must follow CSRF validation.");
        Assert.IsFalse(handler.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(handler.Contains("hmailRequirePost();", StringComparison.Ordinal));

        foreach (var field in new[] { "action", "id", "Active", "DNSHost", "ExpectedResult", "RejectMessage", "Score" })
            StringAssert.Contains(handler, $"hmailGetPostVar(\"{field}\"");

        StringAssert.Contains(handler, "$dnsBlackLists = $obBaseApp->Settings->AntiSpam->DNSBlackLists;");
        StringAssert.Contains(handler, "$dnsBlackLists->ItemByDBID($id)");
        StringAssert.Contains(handler, "$dnsBlackLists->Add();");
        StringAssert.Contains(handler, "$dnsBlackLists->DeleteByDBID($id);");
        StringAssert.Contains(handler, "$dnsBlackList->Active = $Active;");
        StringAssert.Contains(handler, "$dnsBlackList->DNSHost = $DNSHost;");
        StringAssert.Contains(handler, "$dnsBlackList->ExpectedResult = $ExpectedResult;");
        StringAssert.Contains(handler, "$dnsBlackList->RejectMessage = $RejectMessage;");
        StringAssert.Contains(handler, "$dnsBlackList->Score = $Score;");
        StringAssert.Contains(handler, "$dnsBlackList->Save();");
        StringAssert.Contains(handler, "header(\"Location: index.php?page=dnsblacklists\");");

        var editForm = ReadWebAdminSource("hm_dnsblacklist.php");
        StringAssert.Contains(editForm, "method=\"post\"");
        StringAssert.Contains(editForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(editForm, "PrintHidden(\"page\", \"background_dnsblacklist_save\")");
        StringAssert.Contains(editForm, "PrintHidden(\"action\", $action)");
        StringAssert.Contains(editForm, "PrintHidden(\"id\", $id)");
        foreach (var field in new[] { "Active", "DNSHost", "ExpectedResult", "RejectMessage", "Score" })
            StringAssert.Contains(editForm, $"\"{field}\"");

        var deleteForm = ReadWebAdminSource("hm_dnsblacklists.php");
        StringAssert.Contains(deleteForm, "method=\\\"post\\\"");
        StringAssert.Contains(deleteForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(deleteForm, "PrintHidden(\"page\", \"background_dnsblacklist_save\")");
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
