using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminLoggingPostOnlySourceTests
{
    [TestMethod]
    public void LoggingSettingsSaveUsesPostBodyAndRequiresPostCsrf()
    {
        var source = ReadLoggingPage();
        var saveStart = source.IndexOf("if($action == \"save\")", StringComparison.Ordinal);

        Assert.IsTrue(saveStart >= 0, "The logging save branch was not found.");

        var saveBranch = source.Substring(saveStart);

        StringAssert.Contains(source, "$action\t   = hmailGetPostVar(\"action\",\"\");");
        StringAssert.Contains(saveBranch, "hmailRequirePostCsrfToken();");

        foreach (var name in new[]
        {
            "logenabled",
            "logapplication",
            "logsmtp",
            "logpop3",
            "logimap",
            "logtcpip",
            "logdebug",
            "logawstats",
            "KeepFilesOpen"
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

    private static string ReadLoggingPage()
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
                "hm_logging.php");

            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail("Could not locate hmailserver/source/WebAdmin/hm_logging.php from the test output directory.");
        return string.Empty;
    }
}
