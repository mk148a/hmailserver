using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminAccountPostOnlySourceTests
{
    [TestMethod]
    public void AccountMutationUsesPostBodyAndRequiresPostCsrf()
    {
        var source = ReadWebAdminFile("background_account_save.php");
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
            "accountid",
            "action",
            "accountpassword",
            "accountmaxsize",
            "accountaddress",
            "accountactive",
            "accountadminlevel",
            "PersonFirstName",
            "PersonLastName",
            "vacationmessageon",
            "vacationsubject",
            "vacationmessage",
            "vacationmessageexpires",
            "vacationmessageexpiresdate",
            "vacationmessageabortspamflagged",
            "forwardenabled",
            "forwardaddress",
            "forwardkeeporiginal",
            "forwardabortspamflagged",
            "adenabled",
            "addomain",
            "adusername",
            "SignatureEnabled",
            "SignatureHTML",
            "SignaturePlainText"
        })
        {
            StringAssert.Contains(source, $"hmailGetPostVar(\"{field}\"");
        }

        foreach (var accessor in new[]
        {
            "hmailGetPostVar(\"domainid\",0,true)",
            "hmailGetPostVar(\"accountid\",0,true)",
            "hmailGetPostVar(\"action\",\"\")",
            "hmailGetPostVar(\"accountpassword\",\"\")",
            "hmailGetPostVar(\"accountmaxsize\",\"0\")",
            "hmailGetPostVar(\"accountaddress\",\"\")",
            "hmailGetPostVar(\"accountactive\",\"0\")",
            "hmailGetPostVar(\"accountadminlevel\",\"0\")",
            "hmailGetPostVar(\"PersonFirstName\",\"0\")",
            "hmailGetPostVar(\"PersonLastName\",\"0\")",
            "hmailGetPostVar(\"vacationmessageon\",\"\")",
            "hmailGetPostVar(\"vacationsubject\",\"0\")",
            "hmailGetPostVar(\"vacationmessage\",\"\")",
            "hmailGetPostVar(\"vacationmessageexpires\",\"0\")",
            "hmailGetPostVar(\"vacationmessageexpiresdate\",\"2001-01-01\")",
            "hmailGetPostVar(\"vacationmessageabortspamflagged\",\"0\")",
            "hmailGetPostVar(\"forwardenabled\",\"0\")",
            "hmailGetPostVar(\"forwardaddress\",\"\")",
            "hmailGetPostVar(\"forwardkeeporiginal\",\"0\")",
            "hmailGetPostVar(\"forwardabortspamflagged\",\"0\")",
            "hmailGetPostVar(\"adenabled\",\"\")",
            "hmailGetPostVar(\"addomain\",\"0\")",
            "hmailGetPostVar(\"adusername\",\"\")",
            "hmailGetPostVar(\"SignatureEnabled\",\"0\")",
            "hmailGetPostVar(\"SignatureHTML\",\"\")",
            "hmailGetPostVar(\"SignaturePlainText\",\"0\")"
        })
        {
            StringAssert.Contains(source, accessor);
        }

        StringAssert.Contains(
            source,
            "if (hmailGetAdminLevel() == 0 && ($accountid != hmailGetAccountID() || $action != \"edit\"))");
        StringAssert.Contains(
            source,
            "if (hmailGetAdminLevel() == 1 && $domainid != hmailGetDomainID())");
        StringAssert.Contains(source, "$obBaseApp->Domains->ItemByDBID($domainid)");
        StringAssert.Contains(source, "if ($action == \"edit\")");
        StringAssert.Contains(source, "$obDomain->Accounts->ItemByDBID($accountid)");
        StringAssert.Contains(source, "elseif ($action == \"add\")");
        StringAssert.Contains(source, "$obDomain->Accounts->Add()");
        StringAssert.Contains(source, "elseif ($action == \"delete\")");
        StringAssert.Contains(source, "$obDomain->Accounts->DeleteByDBID($accountid)");
        StringAssert.Contains(source, "$_SESSION['session_password'] = $accountpassword;");
        StringAssert.Contains(source, "if ($accountpassword != \"\")");

        foreach (var assignment in new[]
        {
            "$obAccount->Password = \"$accountpassword\";",
            "$obAccount->PersonFirstName = $PersonFirstName;",
            "$obAccount->PersonLastName = $PersonLastName;",
            "$obAccount->VacationMessageIsOn = $vacationmessageon == \"1\";",
            "$obAccount->VacationSubject     = $vacationsubject;",
            "$obAccount->VacationMessage     = $vacationmessage;",
            "$obAccount->VacationMessageExpires      = $vacationmessageexpires;",
            "$obAccount->VacationMessageExpiresDate  = $vacationmessageexpiresdate;",
            "$obAccount->VacationMessageAbortSpamFlagged = $vacationmessageabortspamflagged == \"1\";",
            "$obAccount->ForwardEnabled\t\t= $forwardenabled == \"1\";",
            "$obAccount->ForwardAddress\t   = $forwardaddress;",
            "$obAccount->ForwardKeepOriginal\t= $forwardkeeporiginal == \"1\";",
            "$obAccount->ForwardAbortSpamFlagged = $forwardabortspamflagged == \"1\";",
            "$obAccount->SignatureEnabled\t\t= $SignatureEnabled == \"1\";",
            "$obAccount->SignatureHTML\t\t   = $SignatureHTML;",
            "$obAccount->SignaturePlainText\t= $SignaturePlainText;",
            "$obAccount->Address = $accountaddress;",
            "$obAccount->MaxSize = $accountmaxsize;",
            "$obAccount->Active  = $accountactive;",
            "$obAccount->IsAD         = $adenabled == \"1\";",
            "$obAccount->ADDomain     = $addomain;",
            "$obAccount->ADUsername   = $adusername;"
        })
        {
            StringAssert.Contains(
                normalizedSource,
                System.Text.RegularExpressions.Regex.Replace(assignment, @"\s+", " "));
        }

        StringAssert.Contains(source, "if (hmailGetAdminLevel() != ADMIN_USER)");
        StringAssert.Contains(source, "if (hmailGetAdminLevel() == 1)");
        StringAssert.Contains(source, "else if (hmailGetAdminLevel() == 2)");
        StringAssert.Contains(source, "$obAccount->Save();");
        StringAssert.Contains(source, "$accountid = $obAccount->ID;");
        StringAssert.Contains(
            source,
            "header(\"Location: index.php?page=accounts&domainid=$domainid\");");
        StringAssert.Contains(
            source,
            "header(\"Location: index.php?page=account&action=edit&domainid=$domainid&accountid=$accountid\");");

        var accountForm = ReadWebAdminFile("hm_account.php");
        StringAssert.Contains(accountForm, "method=\"post\"");
        StringAssert.Contains(accountForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(accountForm, "PrintHidden(\"page\", \"background_account_save\")");
        StringAssert.Contains(accountForm, "PrintHidden(\"action\", $action)");
        StringAssert.Contains(accountForm, "PrintHidden(\"domainid\", $obDomain->ID)");
        StringAssert.Contains(accountForm, "PrintHidden(\"accountid\", $accountid)");

        var accountsForm = ReadWebAdminFile("hm_accounts.php");
        Assert.IsTrue(
            accountsForm.Contains("method=\"post\"", StringComparison.Ordinal) ||
            accountsForm.Contains("method=\\\"post\\\"", StringComparison.Ordinal),
            "The account delete form must submit with POST.");
        StringAssert.Contains(accountsForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(accountsForm, "PrintHidden(\"page\", \"background_account_save\")");
        StringAssert.Contains(accountsForm, "PrintHidden(\"action\", \"delete\")");
        StringAssert.Contains(accountsForm, "PrintHidden(\"domainid\", $domainid)");
        StringAssert.Contains(accountsForm, "PrintHidden(\"accountid\", $accountid)");
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
