using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminDomainAliasPostOnlySourceTests
{
    [TestMethod]
    public void DomainAliasMutationUsesPostBodyAndRequiresPostCsrf()
    {
        var handler = ReadWebAdminFile("background_domain_name_save.php");
        var csrfPosition = handler.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstMutationInputPosition = handler.IndexOf("hmailGetPostVar(", StringComparison.Ordinal);

        StringAssert.Contains(handler, "hmailGetAdminLevel() != ADMIN_SERVER");
        Assert.IsTrue(csrfPosition >= 0, "The domain alias handler must require a POST CSRF token.");
        Assert.IsTrue(firstMutationInputPosition > csrfPosition, "Mutation inputs must be read after CSRF validation.");
        Assert.IsFalse(
            handler.Contains("hmailGetVar(", StringComparison.Ordinal),
            "Domain alias mutation inputs must not use the mixed GET/POST accessor.");

        foreach (var name in new[] { "domainid", "aliasid", "action", "aliasname" })
            StringAssert.Contains(handler, $"hmailGetPostVar(\"{name}\"");

        StringAssert.Contains(handler, "$obDomain->DomainAliases->Add()");
        StringAssert.Contains(handler, "$alias->AliasName = $aliasname;");
        StringAssert.Contains(handler, "$alias->Save();");
        StringAssert.Contains(handler, "$obDomain->DomainAliases->DeleteByDBID($aliasid);");

        var form = ReadWebAdminFile("hm_domain_aliasname.php");
        StringAssert.Contains(form, "method=\"post\"");
        StringAssert.Contains(form, "PrintHiddenCsrfToken();");
        StringAssert.Contains(form, "PrintHidden(\"page\", \"background_domain_name_save\")");
        StringAssert.Contains(form, "PrintHidden(\"action\", $action)");
        StringAssert.Contains(form, "PrintHidden(\"domainid\", $domainid)");
        StringAssert.Contains(form, "name=\"aliasname\"");
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
