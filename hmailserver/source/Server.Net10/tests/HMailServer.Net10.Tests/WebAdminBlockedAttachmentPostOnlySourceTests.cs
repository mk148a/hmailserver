using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminBlockedAttachmentPostOnlySourceTests
{
    [TestMethod]
    public void BlockedAttachmentHandlerUsesPostBodyAndRequiresPostCsrfBeforeReads()
    {
        var handler = ReadWebAdminSource("background_blocked_attachment_save.php");
        var authorizationPosition = handler.IndexOf("hmailGetAdminLevel() != ADMIN_SERVER", StringComparison.Ordinal);
        var csrfPosition = handler.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstInputPosition = handler.IndexOf("hmailGetPostVar(", StringComparison.Ordinal);

        Assert.IsTrue(authorizationPosition >= 0, "The blocked attachment handler must retain the server-admin boundary.");
        Assert.IsTrue(csrfPosition > authorizationPosition, "CSRF validation must follow authorization.");
        Assert.IsTrue(firstInputPosition > csrfPosition, "Mutation inputs must be read after CSRF validation.");
        Assert.IsFalse(handler.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(handler.Contains("hmailRequirePost();", StringComparison.Ordinal));

        foreach (var field in new[] { "id", "wildcard", "description", "action" })
            StringAssert.Contains(handler, $"hmailGetPostVar(\"{field}\"");

        StringAssert.Contains(handler, "if ($action == \"add\")");
        StringAssert.Contains(handler, "else if ($action == \"delete\")");
        StringAssert.Contains(handler, "else if ($action == \"edit\")");
        StringAssert.Contains(handler, "$obBaseApp->Settings()");
        StringAssert.Contains(handler, "$obSettings->AntiVirus()");
        StringAssert.Contains(handler, "$obAntivirus->BlockedAttachments");
        StringAssert.Contains(handler, "$blockedAttachments->Add()");
        StringAssert.Contains(handler, "$blockedAttachments->DeleteByDBID($id)");
        StringAssert.Contains(handler, "$blockedAttachments->ItemByDBID($id)");
        StringAssert.Contains(handler, "$blockedAttachment->Wildcard = $wildcard;");
        StringAssert.Contains(handler, "$blockedAttachment->Description = $description;");
        StringAssert.Contains(handler, "$blockedAttachment->Save();");
        StringAssert.Contains(handler, "header(\"Location: index.php?page=smtp_antivirus\");");
    }

    [TestMethod]
    public void BlockedAttachmentMutationFormsUsePostCsrf()
    {
        var editForm = ReadWebAdminSource("hm_blocked_attachment.php");
        StringAssert.Contains(editForm, "method=\"post\"");
        StringAssert.Contains(editForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(editForm, "PrintHidden(\"page\", \"background_blocked_attachment_save\")");

        var antivirusPage = ReadWebAdminSource("hm_smtp_antivirus.php");
        StringAssert.Contains(antivirusPage, "method=\"post\"");
        StringAssert.Contains(antivirusPage, "PrintHiddenCsrfToken();");

        var deleteFormStart = antivirusPage.IndexOf(
            "foreach ($blocked_attachment_delete_form_ids as $id)",
            StringComparison.Ordinal);
        Assert.IsTrue(deleteFormStart >= 0, "The detached blocked attachment delete forms were not found.");

        var deleteForm = antivirusPage.Substring(deleteFormStart);
        StringAssert.Contains(deleteForm, "method=\\\"post\\\"");
        StringAssert.Contains(deleteForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(deleteForm, "PrintHidden(\"page\", \"background_blocked_attachment_save\")");
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
