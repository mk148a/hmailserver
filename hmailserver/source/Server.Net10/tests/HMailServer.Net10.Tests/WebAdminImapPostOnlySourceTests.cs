using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminImapPostOnlySourceTests
{
    [TestMethod]
    public void ImapSaveUsesPostBodyAndRequiresPostCsrf()
    {
        var source = ReadImapPage();
        var saveStart = source.IndexOf("if($action == \"save\")", StringComparison.Ordinal);
        var renderStart = source.IndexOf("$welcomeimap =", saveStart, StringComparison.Ordinal);

        Assert.IsTrue(saveStart >= 0, "The IMAP save branch was not found.");
        Assert.IsTrue(renderStart > saveStart, "The IMAP save branch boundary was not found.");

        var saveBranch = source.Substring(saveStart, renderStart - saveStart);
        var csrfPosition = saveBranch.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstMutationPosition = saveBranch.IndexOf("$obSettings->", StringComparison.Ordinal);

        StringAssert.Contains(source, "hmailGetPostVar(\"action\"");
        StringAssert.Contains(source, "method=\"post\"");
        StringAssert.Contains(source, "PrintHiddenCsrfToken();");
        Assert.IsTrue(csrfPosition >= 0, "The IMAP save branch must require a POST CSRF token.");
        Assert.IsTrue(firstMutationPosition > csrfPosition, "IMAP settings must be written after CSRF validation.");

        foreach (var name in new[]
        {
            "welcomeimap",
            "MaxIMAPConnections",
            "IMAPSortEnabled",
            "IMAPQuotaEnabled",
            "IMAPIdleEnabled",
            "IMAPACLEnabled",
            "IMAPSASLPlainEnabled",
            "IMAPSASLInitialResponseEnabled",
            "IMAPMasterUser",
            "IMAPHierarchyDelimiter"
        })
        {
            StringAssert.Contains(saveBranch, $"hmailGetPostVar(\"{name}\"");
            Assert.IsFalse(
                saveBranch.Contains($"hmailGetVar(\"{name}\"", StringComparison.Ordinal),
                $"IMAP mutation field {name} must not use the mixed GET/POST accessor.");
        }

        foreach (var property in new[]
        {
            "WelcomeIMAP",
            "MaxIMAPConnections",
            "IMAPSortEnabled",
            "IMAPQuotaEnabled",
            "IMAPIdleEnabled",
            "IMAPACLEnabled",
            "IMAPSASLPlainEnabled",
            "IMAPSASLInitialResponseEnabled",
            "IMAPMasterUser",
            "IMAPHierarchyDelimiter"
        })
        {
            StringAssert.Contains(saveBranch, $"$obSettings->{property}");
        }
    }

    private static string ReadImapPage()
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
                "hm_imap.php");

            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail("Could not locate hmailserver/source/WebAdmin/hm_imap.php from the test output directory.");
        return string.Empty;
    }
}
