using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminAutoBanPostOnlySourceTests
{
    [TestMethod]
    public void AutoBanSettingsSaveUsesPostBodyAndRequiresPostCsrf()
    {
        var source = ReadAutoBanPage();
        var saveStart = source.IndexOf("if($action == \"save\")", StringComparison.Ordinal);

        Assert.IsTrue(saveStart >= 0, "The auto-ban save branch was not found.");

        var saveBranch = source.Substring(saveStart);

        StringAssert.Contains(source, "$action\t   = hmailGetPostVar(\"action\",\"\");");
        StringAssert.Contains(saveBranch, "hmailRequirePostCsrfToken();");

        foreach (var name in new[]
        {
            "AutoBanOnLogonFailure",
            "MaxInvalidLogonAttempts",
            "MaxInvalidLogonAttemptsWithin",
            "AutoBanMinutes"
        })
        {
            StringAssert.Contains(saveBranch, $"hmailGetPostVar(\"{name}\"");
            Assert.IsFalse(
                saveBranch.Contains($"hmailGetVar(\"{name}\"", StringComparison.Ordinal),
                $"Mutation field {name} must not use the mixed GET/POST accessor.");
        }

        StringAssert.Contains(source, "method=\"post\"");
        StringAssert.Contains(source, "PrintHiddenCsrfToken();");
    }

    private static string ReadAutoBanPage()
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
                "hm_autoban.php");

            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail("Could not locate hmailserver/source/WebAdmin/hm_autoban.php from the test output directory.");
        return string.Empty;
    }
}
