using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminAliasPostOnlySourceTests
{
    [TestMethod]
    public void AliasMutationUsesPostBodyAndRequiresPostCsrf()
    {
        var source = ReadWebAdminFile("background_alias_save.php");
        var userDenialPosition = source.IndexOf("if (hmailGetAdminLevel() == 0)", StringComparison.Ordinal);
        var csrfPosition = source.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstPostReadPosition = source.IndexOf("hmailGetPostVar(\"domainid\"", StringComparison.Ordinal);

        Assert.IsTrue(userDenialPosition >= 0, "The user-level denial guard was not found.");
        Assert.IsTrue(csrfPosition > userDenialPosition, "CSRF validation must follow the user-level denial guard.");
        Assert.IsTrue(firstPostReadPosition > csrfPosition, "Request values must be read after CSRF validation.");
        Assert.IsFalse(source.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("hmailRequirePost();", StringComparison.Ordinal));

        StringAssert.Contains(source, "hmailHackingAttemp();");
        StringAssert.Contains(source, "if (hmailGetAdminLevel() == 1 && $domainid != hmailGetDomainID())");

        foreach (var field in new[]
        {
            "domainid",
            "aliasid",
            "action",
            "aliasname",
            "aliasvalue",
            "aliasactive"
        })
        {
            StringAssert.Contains(source, $"hmailGetPostVar(\"{field}\"");
        }

        foreach (var accessor in new[]
        {
            "hmailGetPostVar(\"domainid\",0,true)",
            "hmailGetPostVar(\"aliasid\",0)",
            "hmailGetPostVar(\"action\",\"\")",
            "hmailGetPostVar(\"aliasname\",\"\")",
            "hmailGetPostVar(\"aliasvalue\",\"\")",
            "hmailGetPostVar(\"aliasactive\",\"0\")"
        })
        {
            StringAssert.Contains(source, accessor);
        }

        StringAssert.Contains(source, "if ($action == \"edit\")");
        StringAssert.Contains(source, "$obDomain->Aliases->ItemByDBID($aliasid)");
        StringAssert.Contains(source, "elseif ($action == \"add\")");
        StringAssert.Contains(source, "$obDomain->Aliases->Add()");
        StringAssert.Contains(source, "elseif ($action == \"delete\")");
        StringAssert.Contains(source, "$obDomain->Aliases->DeleteByDBID($aliasid);");
        StringAssert.Contains(source, "$obAlias->Name = $aliasname . \"@\" . $domainname;");
        StringAssert.Contains(source, "$obAlias->Value = $aliasvalue;");
        StringAssert.Contains(source, "$obAlias->Active = $aliasactive;");
        StringAssert.Contains(source, "$obAlias->Save();");
        StringAssert.Contains(source, "header(\"Location: index.php?page=aliases&domainid=$domainid\");");
        StringAssert.Contains(source, "header(\"Location: index.php?page=alias&action=edit&domainid=$domainid&aliasid=$aliasid\");");

        var aliasForm = ReadWebAdminFile("hm_alias.php");
        StringAssert.Contains(aliasForm, "method=\"post\"");
        StringAssert.Contains(aliasForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(aliasForm, "PrintHidden(\"page\", \"background_alias_save\")");
        StringAssert.Contains(aliasForm, "PrintHidden(\"action\", $action)");
        StringAssert.Contains(aliasForm, "PrintHidden(\"domainid\", $domainid)");
        StringAssert.Contains(aliasForm, "PrintHidden(\"aliasid\", $aliasid)");

        var aliasesForm = ReadWebAdminFile("hm_aliases.php");
        Assert.IsTrue(
            aliasesForm.Contains("method=\"post\"", StringComparison.Ordinal) ||
            aliasesForm.Contains("method=\\\"post\\\"", StringComparison.Ordinal),
            "The aliases delete form must submit with POST.");
        StringAssert.Contains(aliasesForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(aliasesForm, "PrintHidden(\"page\", \"background_alias_save\")");
        StringAssert.Contains(aliasesForm, "PrintHidden(\"action\", \"delete\")");
        StringAssert.Contains(aliasesForm, "PrintHidden(\"domainid\", $domainid)");
        StringAssert.Contains(aliasesForm, "PrintHidden(\"aliasid\", $aliasid)");
    }

    private static string ReadWebAdminFile(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "hmailserver", "source", "WebAdmin", fileName);
            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail($"Could not locate hmailserver/source/WebAdmin/{fileName} from the test output directory.");
        return string.Empty;
    }
}
