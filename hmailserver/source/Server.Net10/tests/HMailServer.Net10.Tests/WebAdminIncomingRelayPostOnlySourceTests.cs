using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminIncomingRelayPostOnlySourceTests
{
    [TestMethod]
    public void IncomingRelayHandlerUsesPostBodyAndRequiresPostCsrfBeforeMutation()
    {
        var handler = ReadWebAdminSource("background_incomingrelay_save.php");
        var authorizationPosition = handler.IndexOf("hmailGetAdminLevel() != ADMIN_SERVER", StringComparison.Ordinal);
        var csrfPosition = handler.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstInputPosition = handler.IndexOf("hmailGetPostVar(", StringComparison.Ordinal);

        Assert.IsTrue(authorizationPosition >= 0, "The incoming relay handler must retain the server-admin boundary.");
        Assert.IsTrue(csrfPosition > authorizationPosition, "CSRF validation must follow authorization.");
        Assert.IsTrue(firstInputPosition > csrfPosition, "Mutation inputs must be read after CSRF validation.");
        Assert.IsFalse(handler.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(handler.Contains("hmailRequirePost();", StringComparison.Ordinal));

        foreach (var field in new[] { "action", "relayid", "relayname", "relaylowerip", "relayupperip" })
            StringAssert.Contains(handler, $"hmailGetPostVar(\"{field}\"");

        StringAssert.Contains(handler, "$obBaseApp->Settings->IncomingRelays->ItemByDBID($relayid)");
        StringAssert.Contains(handler, "$obBaseApp->Settings->IncomingRelays->Add()");
        StringAssert.Contains(handler, "$obBaseApp->Settings->IncomingRelays->DeleteByDBID($relayid)");
        StringAssert.Contains(handler, "$obIncomingRelay->Name = $relayname;");
        StringAssert.Contains(handler, "$obIncomingRelay->LowerIP = $relaylowerip;");
        StringAssert.Contains(handler, "$obIncomingRelay->UpperIP = $relayupperip;");
        StringAssert.Contains(handler, "$obIncomingRelay->Save();");
        StringAssert.Contains(handler, "header(\"Location: index.php?page=incomingrelays\");");
        StringAssert.Contains(handler, "header(\"Location: index.php?page=incomingrelay&action=edit&relayid=$relayid\");");

        var editForm = ReadWebAdminSource("hm_incomingrelay.php");
        StringAssert.Contains(editForm, "method=\"post\"");
        StringAssert.Contains(editForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(editForm, "PrintHidden(\"page\", \"background_incomingrelay_save\")");

        var deleteForm = ReadWebAdminSource("hm_incomingrelays.php");
        StringAssert.Contains(deleteForm, "method=\\\"post\\\"");
        StringAssert.Contains(deleteForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(deleteForm, "PrintHidden(\"page\", \"background_incomingrelay_save\")");
        StringAssert.Contains(deleteForm, "PrintHidden(\"action\", \"delete\")");
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
