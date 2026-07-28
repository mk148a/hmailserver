using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminDistributionListPostOnlySourceTests
{
    [TestMethod]
    public void DistributionListSaveUsesPostCsrfAndPreservesLegacyMutationSurface()
    {
        var source = ReadWebAdminFile("background_distributionlist_save.php");
        var authPosition = source.IndexOf("if (hmailGetAdminLevel() == 0)", StringComparison.Ordinal);
        var csrfPosition = source.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstPostReadPosition = source.IndexOf("hmailGetPostVar(\"domainid\"", StringComparison.Ordinal);

        Assert.IsTrue(authPosition >= 0, "The user-level denial guard was not found.");
        Assert.IsTrue(csrfPosition > authPosition, "CSRF validation must follow the user-level denial guard.");
        Assert.IsTrue(firstPostReadPosition > csrfPosition, "Request values must be read after CSRF validation.");
        Assert.IsFalse(source.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("hmailRequirePost();", StringComparison.Ordinal));

        StringAssert.Contains(source, "hmailHackingAttemp();");
        StringAssert.Contains(source, "if (hmailGetAdminLevel() == 1 && $domainid != hmailGetDomainID())");

        foreach (var field in new[]
        {
            "domainid",
            "distributionlistid",
            "action",
            "listaddress",
            "listactive",
            "listrequiresmtpauth",
            "RequireSenderAddress",
            "Mode"
        })
        {
            StringAssert.Contains(source, $"hmailGetPostVar(\"{field}\"");
        }

        foreach (var accessor in new[]
        {
            "hmailGetPostVar(\"domainid\",0,true)",
            "hmailGetPostVar(\"distributionlistid\",0)",
            "hmailGetPostVar(\"action\",\"\")",
            "hmailGetPostVar(\"listaddress\",\"\")",
            "hmailGetPostVar(\"listactive\",\"0\")",
            "hmailGetPostVar(\"listrequiresmtpauth\",\"0\")",
            "hmailGetPostVar(\"RequireSenderAddress\",\"\")",
            "hmailGetPostVar(\"Mode\",\"\")"
        })
        {
            StringAssert.Contains(source, accessor);
        }

        StringAssert.Contains(source, "if ($action == \"add\")");
        StringAssert.Contains(source, "IsAddAllowed($obDomain)");
        StringAssert.Contains(source, "if ($result > 0)");
        StringAssert.Contains(source, "STR_DISTRIUBTIONLIST_COULD_NOT_BE_ADDED_MAX_REACHED");
        StringAssert.Contains(source, "header(\"Location: index.php?page=distributionlist&action=$action&domainid=$domainid&distributionlistid=$distributionlistid&error_message=$result\");");
        StringAssert.Contains(source, "if ($action == \"edit\")");
        StringAssert.Contains(source, "$obDomain->DistributionLists->ItemByDBID($distributionlistid)");
        StringAssert.Contains(source, "elseif ($action == \"add\")");
        StringAssert.Contains(source, "$obDomain->DistributionLists->Add()");
        StringAssert.Contains(source, "elseif ($action == \"delete\")");
        StringAssert.Contains(source, "$obDomain->DistributionLists->DeleteByDBID($distributionlistid);");
        StringAssert.Contains(source, "$obList->Address = $listaddress . \"@\" . $domainname;");
        StringAssert.Contains(source, "$obList->RequireSMTPAuth = $listrequiresmtpauth;");
        StringAssert.Contains(source, "$obList->Active = $listactive;");
        StringAssert.Contains(source, "$obList->RequireSenderAddress = $RequireSenderAddress;");
        StringAssert.Contains(source, "$obList->Mode = $Mode;");
        StringAssert.Contains(source, "$obList->Save();");
        StringAssert.Contains(source, "catch(Exception $exception)");
        StringAssert.Contains(source, "ExceptionHandler($exception);");
        StringAssert.Contains(source, "die;");
        StringAssert.Contains(source, "header(\"Location: index.php?page=distributionlists&domainid=$domainid\");");
        StringAssert.Contains(source, "header(\"Location: index.php?page=distributionlist&action=edit&domainid=$domainid&distributionlistid=$distributionlistid\");");

        foreach (var form in new[] { "hm_distributionlist.php", "hm_distributionlists.php" })
        {
            var formSource = ReadWebAdminFile(form);
            Assert.IsTrue(
                formSource.Contains("method=\"post\"", StringComparison.Ordinal) ||
                formSource.Contains("method=\\\"post\\\"", StringComparison.Ordinal),
                $"{form} must submit with POST.");
            StringAssert.Contains(formSource, "PrintHiddenCsrfToken();");
        }
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
