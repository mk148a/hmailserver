using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminDiagnosticsPostOnlySourceTests
{
    [TestMethod]
    public void DiagnosticsPerformTestsUsesPostBodyAndRequiresPostCsrf()
    {
        var source = ReadDiagnosticsPage();
        var actionStart = source.IndexOf("if($action == \"performTests\")", StringComparison.Ordinal);

        Assert.IsTrue(actionStart >= 0, "The diagnostics performTests branch was not found.");

        var actionBranch = source.Substring(actionStart);

        StringAssert.Contains(source, "$action = hmailGetPostVar(\"action\", \"\");");
        StringAssert.Contains(actionBranch, "hmailRequirePostCsrfToken();");
        StringAssert.Contains(actionBranch, "$obDiagnostics->LocalDomainName = hmailGetPostVar(\"LocalDomainName\", \"\");");
        Assert.IsFalse(
            actionBranch.Contains("hmailGetVar(\"LocalDomainName\"", StringComparison.Ordinal),
            "The diagnostics mutation input must not use the mixed GET/POST accessor.");
        StringAssert.Contains(actionBranch, "$obDiagnostics->PerformTests();");
        StringAssert.Contains(source, "method=\"post\"");
        StringAssert.Contains(source, "PrintHiddenCsrfToken();");
    }

    private static string ReadDiagnosticsPage()
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
                "hm_diagnostics.php");

            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail("Could not locate hmailserver/source/WebAdmin/hm_diagnostics.php from the test output directory.");
        return string.Empty;
    }
}
