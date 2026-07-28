using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminSurblServerPostOnlySourceTests
{
    [TestMethod]
    public void SurblServerHandlerUsesPostBodyAndRequiresPostCsrfBeforeMutation()
    {
        var handler = ReadWebAdminSource("background_surblserver_save.php");
        var authorizationPosition = handler.IndexOf("hmailGetAdminLevel() != ADMIN_SERVER", StringComparison.Ordinal);
        var csrfPosition = handler.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstInputPosition = handler.IndexOf("hmailGetPostVar(", StringComparison.Ordinal);
        var settingsPosition = handler.IndexOf("$obBaseApp->Settings->AntiSpam->SURBLServers", StringComparison.Ordinal);

        Assert.IsTrue(authorizationPosition >= 0, "The SURBL handler must retain the server-admin boundary.");
        Assert.IsTrue(csrfPosition > authorizationPosition, "CSRF validation must follow authorization.");
        Assert.IsTrue(firstInputPosition > csrfPosition, "Mutation inputs must be read after CSRF validation.");
        Assert.IsTrue(settingsPosition > csrfPosition, "SURBL settings access must follow CSRF validation.");
        Assert.IsFalse(handler.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(handler.Contains("hmailRequirePost();", StringComparison.Ordinal));

        foreach (var field in new[] { "action", "id", "Active", "DNSHost", "RejectMessage", "Score" })
            StringAssert.Contains(handler, $"hmailGetPostVar(\"{field}\"");

        StringAssert.Contains(handler, "$surblServers->ItemByDBID($id)");
        StringAssert.Contains(handler, "$surblServers->Add();");
        StringAssert.Contains(handler, "$surblServers->DeleteByDBID($id);");
        StringAssert.Contains(handler, "$surblServer->Active = $Active;");
        StringAssert.Contains(handler, "$surblServer->DNSHost = $DNSHost;");
        StringAssert.Contains(handler, "$surblServer->RejectMessage = $RejectMessage;");
        StringAssert.Contains(handler, "$surblServer->Score = $Score;");
        StringAssert.Contains(handler, "$surblServer->Save();");
        StringAssert.Contains(handler, "header(\"Location: index.php?page=surblservers\");");

        var editForm = ReadWebAdminSource("hm_surblserver.php");
        StringAssert.Contains(editForm, "method=\"post\"");
        StringAssert.Contains(editForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(editForm, "PrintHidden(\"page\", \"background_surblserver_save\")");
        StringAssert.Contains(editForm, "PrintHidden(\"action\", \"$action\")");
        StringAssert.Contains(editForm, "PrintHidden(\"id\", \"$id\")");
        foreach (var field in new[] { "Active", "DNSHost", "RejectMessage", "Score" })
            StringAssert.Contains(editForm, $"\"{field}\"");

        var deleteForm = ReadWebAdminSource("hm_surblservers.php");
        StringAssert.Contains(deleteForm, "method=\\\"post\\\"");
        StringAssert.Contains(deleteForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(deleteForm, "PrintHidden(\"page\", \"background_surblserver_save\")");
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
