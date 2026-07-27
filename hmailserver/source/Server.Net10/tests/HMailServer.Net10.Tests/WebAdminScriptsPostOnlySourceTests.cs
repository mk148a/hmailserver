using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminScriptsPostOnlySourceTests
{
    [TestMethod]
    public void ScriptActionsUsePostBodyAndRequirePostCsrf()
    {
        var source = ReadScriptsPage();

        StringAssert.Contains(source, "$action\t   = hmailGetPostVar(\"action\",\"\");");

        var saveStart = source.IndexOf("if($action == \"save\")", StringComparison.Ordinal);
        var checkSyntaxStart = source.IndexOf("elseif ($action == \"checksyntax\")", StringComparison.Ordinal);
        var reloadStart = source.IndexOf("elseif ($action == \"reloadscripts\")", StringComparison.Ordinal);

        Assert.IsTrue(saveStart >= 0, "The scripts save branch was not found.");
        Assert.IsTrue(checkSyntaxStart > saveStart, "The scripts CheckSyntax branch was not found after save.");
        Assert.IsTrue(reloadStart > checkSyntaxStart, "The scripts Reload branch was not found after CheckSyntax.");

        var saveBranch = source.Substring(saveStart, checkSyntaxStart - saveStart);
        var checkSyntaxBranch = source.Substring(checkSyntaxStart, reloadStart - checkSyntaxStart);
        var reloadBranch = source.Substring(reloadStart);

        StringAssert.Contains(saveBranch, "hmailRequirePostCsrfToken();");
        StringAssert.Contains(checkSyntaxBranch, "hmailRequirePostCsrfToken();");
        StringAssert.Contains(reloadBranch, "hmailRequirePostCsrfToken();");
        StringAssert.Contains(checkSyntaxBranch, "$obScripting->CheckSyntax();");
        StringAssert.Contains(reloadBranch, "$obScripting->Reload();");

        foreach (var name in new[] { "scriptingenabled", "scriptinglanguage" })
        {
            StringAssert.Contains(saveBranch, $"hmailGetPostVar(\"{name}\"");
            Assert.IsFalse(
                saveBranch.Contains($"hmailGetVar(\"{name}\"", StringComparison.Ordinal),
                $"Mutation field {name} must not use the mixed GET/POST accessor.");
        }

        StringAssert.Contains(source, "method=\"post\"");
        StringAssert.Contains(source, "PrintHiddenCsrfToken();");
    }

    private static string ReadScriptsPage()
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
                "hm_scripts.php");

            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail("Could not locate hmailserver/source/WebAdmin/hm_scripts.php from the test output directory.");
        return string.Empty;
    }
}
