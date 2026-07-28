using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminMirrorPostOnlySourceTests
{
    [TestMethod]
    public void MirrorSaveUsesPostBodyAndRequiresPostCsrf()
    {
        var source = ReadMirrorPage();
        var saveStart = source.IndexOf("if($action == \"save\")", StringComparison.Ordinal);
        var renderStart = source.IndexOf("$mirroremailaddress =", saveStart, StringComparison.Ordinal);

        Assert.IsTrue(saveStart >= 0, "The Mirror save branch was not found.");
        Assert.IsTrue(renderStart > saveStart, "The Mirror save branch boundary was not found.");

        var saveBranch = source.Substring(saveStart, renderStart - saveStart);
        var csrfPosition = saveBranch.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstMutationPosition = saveBranch.IndexOf("$obSettings->MirrorEMailAddress", StringComparison.Ordinal);

        StringAssert.Contains(source, "hmailGetPostVar(\"action\"");
        StringAssert.Contains(source, "method=\"post\"");
        StringAssert.Contains(source, "PrintHiddenCsrfToken();");
        Assert.IsTrue(csrfPosition >= 0, "The Mirror save branch must require a POST CSRF token.");
        Assert.IsTrue(firstMutationPosition > csrfPosition, "Mirror settings must be written after CSRF validation.");
        Assert.IsFalse(
            saveBranch.Contains("hmailGetVar(", StringComparison.Ordinal),
            "Mirror mutation inputs must not use the mixed GET/POST accessor.");
        StringAssert.Contains(saveBranch, "hmailGetPostVar(\"mirroremailaddress\"");
        StringAssert.Contains(saveBranch, "$obSettings->MirrorEMailAddress");
    }

    private static string ReadMirrorPage()
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
                "hm_mirror.php");

            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail("Could not locate hmailserver/source/WebAdmin/hm_mirror.php from the test output directory.");
        return string.Empty;
    }
}
