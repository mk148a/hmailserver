using System.Text;
namespace HMailServer.Net10.Tests;
[TestClass]
public sealed class WebAdminAjaxVirusTestPostOnlySourceTests
{
    [TestMethod]
    public void AjaxVirusTestHandlerRequiresPostAndCsrf()
    {
        var source = ReadFile("background_ajax_virustest.php");
        var auth = source.IndexOf("hmailGetAdminLevel() != ADMIN_SERVER", 0, StringComparison.Ordinal);
        var csrf = source.IndexOf("hmailRequirePostCsrfToken();", 0, StringComparison.Ordinal);
        var read = source.IndexOf("hmailGetPostVar(", 0, StringComparison.Ordinal);
        Assert.IsTrue(auth >= 0, "server-admin guard missing");
        Assert.IsTrue(csrf > auth, "csrf must follow guard");
        Assert.IsTrue(read > csrf, "reads must follow csrf");
        Assert.IsFalse(source.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("hmailRequirePost();", StringComparison.Ordinal));
    }
    private static string ReadFile(string name)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "hmailserver", "source", "WebAdmin", name);
            if (File.Exists(p)) return File.ReadAllText(p, Encoding.UTF8);
        }
        Assert.Fail("could not locate " + name);
        return string.Empty;
    }
}
