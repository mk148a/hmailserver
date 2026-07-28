using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminDistributionListRecipientPostOnlySourceTests
{
    [TestMethod]
    public void DistributionListRecipientHandlerUsesPostBodyAndRequiresPostCsrfBeforeMutation()
    {
        var handler = ReadWebAdminSource("background_distributionlist_recipient_save.php");
        var authorizationPosition = handler.IndexOf("hmailGetAdminLevel() == 0", StringComparison.Ordinal);
        var csrfPosition = handler.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstInputPosition = handler.IndexOf("hmailGetPostVar(", StringComparison.Ordinal);

        Assert.IsTrue(authorizationPosition >= 0, "The recipient handler must retain the user-level boundary.");
        Assert.IsTrue(csrfPosition > authorizationPosition, "CSRF validation must follow the user-level denial.");
        Assert.IsTrue(firstInputPosition > csrfPosition, "Mutation inputs must be read after CSRF validation.");
        Assert.IsFalse(handler.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(handler.Contains("hmailRequirePost();", StringComparison.Ordinal));

        foreach (var field in new[] { "distributionlistid", "recipientid", "domainid", "action", "recipientaddress" })
            StringAssert.Contains(handler, $"hmailGetPostVar(\"{field}\"");

        StringAssert.Contains(handler, "$obBaseApp->Domains->ItemByDBID($domainid)");
        StringAssert.Contains(handler, "$obDomain->DistributionLists->ItemByDBID($distributionlistid)");
        StringAssert.Contains(handler, "$obList->Recipients->ItemByDBID($recipientid)");
        StringAssert.Contains(handler, "$obList->Recipients->Add()");
        StringAssert.Contains(handler, "$obRecipient->Delete();");
        StringAssert.Contains(handler, "$obRecipient->RecipientAddress = $recipientaddress;");
        StringAssert.Contains(handler, "$obRecipient->Save();");
        StringAssert.Contains(handler, "header(\"Location: index.php?page=distributionlist_recipients&domainid=$domainid&distributionlistid=$distributionlistid\");");
        StringAssert.Contains(handler, "header(\"Location: index.php?page=distributionlist_recipient&action=edit&domainid=$domainid&distributionlistid=$distributionlistid&recipientid=$recipientid\");");
    }

    [TestMethod]
    public void DistributionListRecipientMutationFormsUsePostCsrf()
    {
        var editForm = ReadWebAdminSource("hm_distributionlist_recipient.php");
        StringAssert.Contains(editForm, "method=\"post\"");
        StringAssert.Contains(editForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(editForm, "PrintHidden(\"page\", \"background_distributionlist_recipient_save\")");
        StringAssert.Contains(editForm, "PrintHidden(\"distributionlistid\", $distributionlistid)");
        StringAssert.Contains(editForm, "PrintHidden(\"domainid\", $domainid)");
        StringAssert.Contains(editForm, "PrintHidden(\"recipientid\", $recipientid)");

        var recipientsPage = ReadWebAdminSource("hm_distributionlist_recipients.php");
        var deleteFormStart = recipientsPage.IndexOf(
            "echo \"<form action=\\\"index.php\\\" method=\\\"post\\\"",
            StringComparison.Ordinal);
        Assert.IsTrue(deleteFormStart >= 0, "The distribution-list recipient delete form was not found.");

        var deleteForm = recipientsPage.Substring(deleteFormStart);
        StringAssert.Contains(deleteForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(deleteForm, "PrintHidden(\"page\", \"background_distributionlist_recipient_save\")");
        StringAssert.Contains(deleteForm, "PrintHidden(\"action\", \"delete\")");
        StringAssert.Contains(deleteForm, "PrintHidden(\"domainid\", $domainid)");
        StringAssert.Contains(deleteForm, "PrintHidden(\"distributionlistid\", $distributionlistid)");
        StringAssert.Contains(deleteForm, "PrintHidden(\"recipientid\", $recipientid)");
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
