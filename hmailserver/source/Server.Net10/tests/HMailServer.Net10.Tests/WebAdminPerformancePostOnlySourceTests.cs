using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminPerformancePostOnlySourceTests
{
    [TestMethod]
    public void PerformanceMutationsUsePostBodyAndRequirePostCsrf()
    {
        var source = ReadPerformancePage();
        var saveStart = source.IndexOf("if($action == \"save\")", StringComparison.Ordinal);
        var clearStart = source.IndexOf(
            "else if ($action == \"ClearMessageIndexingCache\")",
            saveStart,
            StringComparison.Ordinal);

        Assert.IsTrue(saveStart >= 0, "The performance save branch was not found.");
        Assert.IsTrue(clearStart > saveStart, "The performance clear branch was not found.");

        var saveBranch = source.Substring(saveStart, clearStart - saveStart);
        var clearBranch = source.Substring(clearStart);

        StringAssert.Contains(source, "$action\t   = hmailGetPostVar(\"action\",\"\");");
        StringAssert.Contains(saveBranch, "hmailRequirePostCsrfToken();");
        StringAssert.Contains(clearBranch, "hmailRequirePostCsrfToken();");

        foreach (var name in new[]
        {
            "cacheenabled",
            "cachedomainttl",
            "cacheaccountttl",
            "cachealiasttl",
            "cachedistributionlistttl",
            "tcpipthreads",
            "maxdeliverythreads",
            "MaxAsynchronousThreads",
            "workerthreadpriority",
            "MessageIndexingEnabled"
        })
        {
            StringAssert.Contains(saveBranch, $"hmailGetPostVar(\"{name}\"");
            Assert.IsFalse(
                saveBranch.Contains($"hmailGetVar(\"{name}\"", StringComparison.Ordinal),
                $"Mutation field {name} must not use the mixed GET/POST accessor.");
        }
    }

    private static string ReadPerformancePage()
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
                "hm_performance.php");

            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail("Could not locate hmailserver/source/WebAdmin/hm_performance.php from the test output directory.");
        return string.Empty;
    }
}
