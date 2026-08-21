using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminLogoutPostOnlySourceTests
{
    [TestMethod]
    public void LogoutHandlerValidatesPostCsrfBeforeDestroyingSession()
    {
        var source = ReadWebAdminFile("logout.php");
        var csrfPosition = source.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var destroyPosition = source.IndexOf("session_destroy();", StringComparison.Ordinal);

        Assert.IsTrue(csrfPosition >= 0, "The logout handler must require a POST CSRF token.");
        Assert.IsTrue(destroyPosition > csrfPosition, "CSRF validation must precede session destruction.");
        Assert.IsFalse(destroyPosition < csrfPosition, "Session destruction must not precede CSRF validation.");
    }

    [TestMethod]
    public void LogoutHandlerHasNoGetOrInitializationComPath()
    {
        var source = ReadWebAdminFile("logout.php");

        Assert.IsFalse(source.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("$_GET", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("$_REQUEST", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("initialize.php", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("new COM", StringComparison.Ordinal));
    }

    [TestMethod]
    public void LogoutCallersUsePostFormsWithCsrfTokens()
    {
        var treeSource = ReadWebAdminFile("include_treemenu.php");
        var errorSource = ReadWebAdminFile("error.php");

        foreach (var source in new[] { treeSource, errorSource })
        {
            StringAssert.Contains(source, "action=\"logout.php\"");
            StringAssert.Contains(source, "method=\"post\"");
            StringAssert.Contains(source, "PrintHiddenCsrfToken();");
        }

        StringAssert.Contains(treeSource, "document.forms.logoutform.submit();");
        Assert.IsFalse(treeSource.Contains(",'logout.php',", StringComparison.Ordinal));
        Assert.IsFalse(errorSource.Contains("document.location.href='logout.php'", StringComparison.Ordinal));
    }

    [TestMethod]
    public void LogoutPreservesSessionDestructionAndRedirect()
    {
        var source = ReadWebAdminFile("logout.php");

        StringAssert.Contains(source, "session_destroy();");
        StringAssert.Contains(source, "$hmail_config['rooturl'] . \"index.php\"");
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
