using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminPop3PostOnlySourceTests
{
    [TestMethod]
    public void Pop3SaveUsesPostBodyAndRequiresPostCsrf()
    {
        var source = ReadPop3Page();
        var saveStart = source.IndexOf("if($action == \"save\")", StringComparison.Ordinal);
        var renderStart = source.IndexOf("$maxpop3connections =", saveStart, StringComparison.Ordinal);

        Assert.IsTrue(saveStart >= 0, "The POP3 save branch was not found.");
        Assert.IsTrue(renderStart > saveStart, "The POP3 save branch boundary was not found.");

        var saveBranch = source.Substring(saveStart, renderStart - saveStart);
        var csrfPosition = saveBranch.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstMutationPosition = saveBranch.IndexOf("$obSettings->", StringComparison.Ordinal);

        StringAssert.Contains(source, "hmailGetPostVar(\"action\"");
        StringAssert.Contains(source, "method=\"post\"");
        StringAssert.Contains(source, "PrintHiddenCsrfToken();");
        Assert.IsTrue(csrfPosition >= 0, "The POP3 save branch must require a POST CSRF token.");
        Assert.IsTrue(firstMutationPosition > csrfPosition, "POP3 settings must be written after CSRF validation.");

        foreach (var name in new[] { "maxpop3connections", "welcomepop3" })
        {
            StringAssert.Contains(saveBranch, $"hmailGetPostVar(\"{name}\"");
            Assert.IsFalse(
                saveBranch.Contains($"hmailGetVar(\"{name}\"", StringComparison.Ordinal),
                $"POP3 mutation field {name} must not use the mixed GET/POST accessor.");
        }

        StringAssert.Contains(saveBranch, "$obSettings->MaxPOP3Connections");
        StringAssert.Contains(saveBranch, "$obSettings->WelcomePOP3");
    }

    private static string ReadPop3Page()
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
                "hm_pop3.php");

            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail("Could not locate hmailserver/source/WebAdmin/hm_pop3.php from the test output directory.");
        return string.Empty;
    }
}
