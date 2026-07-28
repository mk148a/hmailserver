using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminDomainPostOnlySourceTests
{
    [TestMethod]
    public void DomainMutationUsesPostBodyAndRequiresPostCsrf()
    {
        var source = ReadWebAdminFile("background_domain_save.php");
        var normalizedSource = System.Text.RegularExpressions.Regex.Replace(source, @"\s+", " ");
        var webAdminGuardPosition = source.IndexOf(
            "if (!defined('IN_WEBADMIN'))",
            StringComparison.Ordinal);
        var csrfPosition = source.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstPostReadPosition = source.IndexOf(
            "hmailGetPostVar(\"domainid\"",
            StringComparison.Ordinal);

        Assert.IsTrue(webAdminGuardPosition >= 0, "The WebAdmin entry guard was not found.");
        Assert.IsTrue(csrfPosition > webAdminGuardPosition, "CSRF validation must follow the WebAdmin entry guard.");
        Assert.IsTrue(firstPostReadPosition > csrfPosition, "Request values must be read after CSRF validation.");
        Assert.IsFalse(source.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("hmailRequirePost();", StringComparison.Ordinal));

        foreach (var field in new[]
        {
            "domainid",
            "action",
            "domainname",
            "domainactive",
            "domainpostmaster",
            "domainmaxsize",
            "domainmaxmessagesize",
            "domainplusaddressingenabled",
            "domainplusaddressingcharacter",
            "domainantispamenablegreylisting",
            "SignatureEnabled",
            "SignatureHTML",
            "SignaturePlainText",
            "SignatureMethod",
            "AddSignaturesToLocalMail",
            "AddSignaturesToReplies",
            "MaxAccountSize",
            "MaxNumberOfAccounts",
            "MaxNumberOfAliases",
            "MaxNumberOfDistributionLists",
            "MaxNumberOfAccountsEnabled",
            "MaxNumberOfAliasesEnabled",
            "MaxNumberOfDistributionListsEnabled",
            "DKIMSignEnabled",
            "DKIMSignAliasesEnabled",
            "DKIMPrivateKeyFile",
            "DKIMSelector",
            "DKIMHeaderCanonicalizationMethod",
            "DKIMBodyCanonicalizationMethod",
            "DKIMSigningAlgorithm"
        })
        {
            StringAssert.Contains(source, $"hmailGetPostVar(\"{field}\"");
        }

        foreach (var accessor in new[]
        {
            "hmailGetPostVar(\"domainid\",0,true)",
            "hmailGetPostVar(\"action\",\"\")",
            "hmailGetPostVar(\"domainname\",\"\")",
            "hmailGetPostVar(\"domainactive\",\"0\")",
            "hmailGetPostVar(\"domainpostmaster\",\"\")",
            "hmailGetPostVar(\"domainmaxsize\",\"0\")",
            "hmailGetPostVar(\"domainmaxmessagesize\",\"0\")",
            "hmailGetPostVar(\"domainplusaddressingenabled\",\"0\")",
            "hmailGetPostVar(\"domainplusaddressingcharacter\",\"+\")",
            "hmailGetPostVar(\"domainantispamenablegreylisting\",\"0\")",
            "hmailGetPostVar(\"SignatureEnabled\",\"0\")",
            "hmailGetPostVar(\"SignatureHTML\",\"\")",
            "hmailGetPostVar(\"SignaturePlainText\",\"\")",
            "hmailGetPostVar(\"SignatureMethod\",\"1\")",
            "hmailGetPostVar(\"AddSignaturesToLocalMail\",\"0\")",
            "hmailGetPostVar(\"AddSignaturesToReplies\",\"0\")",
            "hmailGetPostVar(\"MaxAccountSize\",\"0\")",
            "hmailGetPostVar(\"MaxNumberOfAccounts\",\"0\")",
            "hmailGetPostVar(\"MaxNumberOfAliases\",\"0\")",
            "hmailGetPostVar(\"MaxNumberOfDistributionLists\",\"0\")",
            "hmailGetPostVar(\"MaxNumberOfAccountsEnabled\",\"0\")",
            "hmailGetPostVar(\"MaxNumberOfAliasesEnabled\",\"0\")",
            "hmailGetPostVar(\"MaxNumberOfDistributionListsEnabled\",\"0\")",
            "hmailGetPostVar(\"DKIMSignEnabled\", \"0\")",
            "hmailGetPostVar(\"DKIMSignAliasesEnabled\", \"0\")",
            "hmailGetPostVar(\"DKIMPrivateKeyFile\", \"\")",
            "hmailGetPostVar(\"DKIMSelector\", \"\")",
            "hmailGetPostVar(\"DKIMHeaderCanonicalizationMethod\", \"2\")",
            "hmailGetPostVar(\"DKIMBodyCanonicalizationMethod\", \"2\")",
            "hmailGetPostVar(\"DKIMSigningAlgorithm\", \"2\")"
        })
        {
            StringAssert.Contains(normalizedSource, accessor);
        }

        StringAssert.Contains(
            source,
            "if (hmailGetAdminLevel() == 1 && ($domainid != hmailGetDomainID() || $action != \"edit\"))");
        StringAssert.Contains(source, "if ($action == \"edit\")");
        StringAssert.Contains(source, "$obBaseApp->Domains->ItemByDBID($domainid)");
        StringAssert.Contains(source, "elseif ($action == \"add\")");
        StringAssert.Contains(source, "$obBaseApp->Domains->Add()");
        StringAssert.Contains(source, "elseif ($action == \"delete\")");
        StringAssert.Contains(source, "if (hmailGetAdminLevel() != ADMIN_SERVER)");
        StringAssert.Contains(source, "$obDomain->Delete();");

        foreach (var assignment in new[]
        {
            "$obDomain->Postmaster = $domainpostmaster;",
            "$obDomain->PlusAddressingEnabled = $domainplusaddressingenabled == \"1\";",
            "$obDomain->PlusAddressingCharacter = $domainplusaddressingcharacter;",
            "$obDomain->AntiSpamEnableGreylisting = $domainantispamenablegreylisting == \"1\";",
            "$obDomain->SignatureEnabled = $SignatureEnabled == \"1\";",
            "$obDomain->SignaturePlainText = $SignaturePlainText;",
            "$obDomain->SignatureHTML = $SignatureHTML;",
            "$obDomain->SignatureMethod = $SignatureMethod;",
            "$obDomain->AddSignaturesToLocalMail = $AddSignaturesToLocalMail;",
            "$obDomain->AddSignaturesToReplies = $AddSignaturesToReplies;",
            "$obDomain->DKIMSignEnabled = $DKIMSignEnabled;",
            "$obDomain->DKIMSignAliasesEnabled = $DKIMSignAliasesEnabled;",
            "$obDomain->DKIMPrivateKeyFile = $DKIMPrivateKeyFile;",
            "$obDomain->DKIMSelector = $DKIMSelector;",
            "$obDomain->DKIMHeaderCanonicalizationMethod = $DKIMHeaderCanonicalizationMethod;",
            "$obDomain->DKIMBodyCanonicalizationMethod = $DKIMBodyCanonicalizationMethod;",
            "$obDomain->DKIMSigningAlgorithm = $DKIMSigningAlgorithm;",
            "$obDomain->Active = $domainactive;",
            "$obDomain->Name = $domainname;",
            "$obDomain->MaxSize = $domainmaxsize;",
            "$obDomain->MaxMessageSize = $domainmaxmessagesize;",
            "$obDomain->MaxAccountSize = $MaxAccountSize;",
            "$obDomain->MaxNumberOfAccounts = $MaxNumberOfAccounts;",
            "$obDomain->MaxNumberOfAliases = $MaxNumberOfAliases;",
            "$obDomain->MaxNumberOfDistributionLists = $MaxNumberOfDistributionLists;",
            "$obDomain->MaxNumberOfAccountsEnabled = $MaxNumberOfAccountsEnabled;",
            "$obDomain->MaxNumberOfAliasesEnabled = $MaxNumberOfAliasesEnabled;",
            "$obDomain->MaxNumberOfDistributionListsEnabled = $MaxNumberOfDistributionListsEnabled;"
        })
        {
            StringAssert.Contains(
                normalizedSource,
                System.Text.RegularExpressions.Regex.Replace(assignment, @"\s+", " "));
        }

        StringAssert.Contains(source, "if ($obDomain->DomainAliases->Count > 0)");
        StringAssert.Contains(source, "$obDomain->Save();");
        StringAssert.Contains(source, "$domainid = $obDomain->ID;");
        StringAssert.Contains(source, "header(\"Location: index.php?page=domains\")");
        StringAssert.Contains(
            source,
            "header(\"Location: index.php?page=domain&action=edit&domainid=$domainid\")");

        var domainForm = ReadWebAdminFile("hm_domain.php");
        StringAssert.Contains(domainForm, "method=\"post\"");
        StringAssert.Contains(domainForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(domainForm, "PrintHidden(\"page\", \"background_domain_save\")");
        StringAssert.Contains(domainForm, "PrintHidden(\"action\", $action)");
        StringAssert.Contains(domainForm, "PrintHidden(\"domainid\", $DomainID)");

        var domainsForm = ReadWebAdminFile("hm_domains.php");
        Assert.IsTrue(
            domainsForm.Contains("method=\"post\"", StringComparison.Ordinal) ||
            domainsForm.Contains("method=\\\"post\\\"", StringComparison.Ordinal),
            "The domain delete form must submit with POST.");
        StringAssert.Contains(domainsForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(domainsForm, "PrintHidden(\"page\", \"background_domain_save\")");
        StringAssert.Contains(domainsForm, "PrintHidden(\"action\", \"delete\")");
        StringAssert.Contains(domainsForm, "PrintHidden(\"domainid\", $domainid)");
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
