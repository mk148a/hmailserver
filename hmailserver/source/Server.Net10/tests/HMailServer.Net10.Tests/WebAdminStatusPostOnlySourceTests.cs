using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminStatusPostOnlySourceTests
{
    [TestMethod]
    public void StatusControlUsesPostBodyAndRequiresPostCsrf()
    {
        var source = ReadStatusPage();
        var controlStart = source.IndexOf("if ($action == \"control\")", StringComparison.Ordinal);
        var switchStart = source.IndexOf("switch($serverstate)", controlStart, StringComparison.Ordinal);

        Assert.IsTrue(controlStart >= 0, "The status control branch was not found.");
        Assert.IsTrue(switchStart > controlStart, "The status control branch boundary was not found.");

        var controlBranch = source.Substring(controlStart, switchStart - controlStart);
        var csrfPosition = controlBranch.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var startPosition = controlBranch.IndexOf("$obBaseApp->Start();", StringComparison.Ordinal);
        var stopPosition = controlBranch.IndexOf("$obBaseApp->Stop();", StringComparison.Ordinal);

        StringAssert.Contains(source, "hmailGetPostVar(\"action\"");
        StringAssert.Contains(controlBranch, "hmailGetPostVar(\"controlaction\"");
        Assert.IsFalse(
            controlBranch.Contains("hmailGetVar(\"controlaction\"", StringComparison.Ordinal),
            "The server control action must not use the mixed GET/POST accessor.");
        Assert.IsTrue(csrfPosition >= 0, "The status control branch must require a POST CSRF token.");
        Assert.IsTrue(startPosition > csrfPosition, "Start must occur after CSRF validation.");
        Assert.IsTrue(stopPosition > csrfPosition, "Stop must occur after CSRF validation.");
        StringAssert.Contains(source, "method=\"post\"");
        StringAssert.Contains(source, "PrintHiddenCsrfToken();");
    }

    private static string ReadStatusPage()
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
                "hm_status.php");

            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail("Could not locate hmailserver/source/WebAdmin/hm_status.php from the test output directory.");
        return string.Empty;
    }
}
