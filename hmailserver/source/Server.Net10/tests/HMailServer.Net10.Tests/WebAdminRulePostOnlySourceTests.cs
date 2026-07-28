using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminRulePostOnlySourceTests
{
    [TestMethod]
    public void RuleMutationUsesPostBodyAndRequiresPostCsrf()
    {
        var source = ReadWebAdminFile("background_rule_save.php");
        var normalizedSource = System.Text.RegularExpressions.Regex.Replace(source, @"\s+", " ");
        var webAdminGuardPosition = source.IndexOf(
            "if (!defined('IN_WEBADMIN'))",
            StringComparison.Ordinal);
        var csrfPosition = source.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstPostReadPosition = source.IndexOf(
            "hmailGetPostVar(\"action\"",
            StringComparison.Ordinal);

        Assert.IsTrue(webAdminGuardPosition >= 0, "The WebAdmin entry guard was not found.");
        Assert.IsTrue(csrfPosition > webAdminGuardPosition, "CSRF validation must follow the WebAdmin entry guard.");
        Assert.IsTrue(firstPostReadPosition > csrfPosition, "Request values must be read after CSRF validation.");
        Assert.IsFalse(source.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("hmailRequirePost();", StringComparison.Ordinal));

        foreach (var field in new[]
        {
            "action",
            "domainid",
            "accountid",
            "ruleid",
            "criteriaid",
            "actionid",
            "savetype",
            "UsePredefined",
            "PredefinedField",
            "MatchType",
            "MatchValue",
            "HeaderField",
            "Type",
            "To",
            "IMAPFolder",
            "ScriptFunction",
            "FromName",
            "FromAddress",
            "Subject",
            "Body",
            "HeaderName",
            "replyabortspamflagged",
            "forwardabortspamflagged",
            "Value",
            "BindToAddress",
            "Name",
            "Active",
            "UseAND"
        })
        {
            StringAssert.Contains(source, $"hmailGetPostVar(\"{field}\"");
        }

        foreach (var accessor in new[]
        {
            "hmailGetPostVar(\"action\",\"\")",
            "hmailGetPostVar(\"domainid\", 0, true)",
            "hmailGetPostVar(\"accountid\", 0, true)",
            "hmailGetPostVar(\"ruleid\", 0)",
            "hmailGetPostVar(\"criteriaid\", 0)",
            "hmailGetPostVar(\"actionid\", 0)",
            "hmailGetPostVar(\"savetype\", 0)",
            "hmailGetPostVar(\"UsePredefined\", 0)",
            "hmailGetPostVar(\"PredefinedField\", 0)",
            "hmailGetPostVar(\"MatchType\", 0)",
            "hmailGetPostVar(\"MatchValue\", 0)",
            "hmailGetPostVar(\"HeaderField\", 0)",
            "hmailGetPostVar(\"Type\", 0)",
            "hmailGetPostVar(\"To\", \"\")",
            "hmailGetPostVar(\"IMAPFolder\", \"\")",
            "hmailGetPostVar(\"ScriptFunction\", \"\")",
            "hmailGetPostVar(\"FromName\", \"\")",
            "hmailGetPostVar(\"FromAddress\", \"\")",
            "hmailGetPostVar(\"Subject\", \"\")",
            "hmailGetPostVar(\"Body\", \"\")",
            "hmailGetPostVar(\"HeaderName\", \"\")",
            "hmailGetPostVar(\"replyabortspamflagged\", \"0\")",
            "hmailGetPostVar(\"forwardabortspamflagged\", \"0\")",
            "hmailGetPostVar(\"Value\", \"\")",
            "hmailGetPostVar(\"BindToAddress\", \"\")",
            "hmailGetPostVar(\"Name\", \"\")",
            "hmailGetPostVar(\"Active\", \"\")",
            "hmailGetPostVar(\"UseAND\", \"\")"
        })
        {
            StringAssert.Contains(source, accessor);
        }

        StringAssert.Contains(source, "if (!GetHasRuleAccess($domainid, $accountid))");
        StringAssert.Contains(source, "hmailHackingAttemp();");
        StringAssert.Contains(source, "include \"include/rule_strings.php\";");
        StringAssert.Contains(source, "$rule_link = \"index.php?page=rule&action=edit&domainid=$domainid&accountid=$accountid&ruleid=$ruleid\";");

        StringAssert.Contains(source, "if ($action == \"add\" && $savetype == \"rule\")");
        StringAssert.Contains(source, "$obBaseApp->Rules->Add()");
        StringAssert.Contains(source, "$obBaseApp->Domains->ItemByDBID($domainid)->Accounts->ItemByDBID($accountid)->Rules->Add()");
        StringAssert.Contains(source, "$obBaseApp->Rules->ItemByDBID($ruleid)");
        StringAssert.Contains(source, "$obBaseApp->Domains->ItemByDBID($domainid)->Accounts->ItemByDBID($accountid)->Rules->ItemByDBID($ruleid)");

        StringAssert.Contains(source, "if ($action == \"delete\")");
        StringAssert.Contains(source, "$rule->Criterias->ItemByDBID($criteriaid)->Delete();");
        StringAssert.Contains(source, "$rule->Actions->ItemByDBID($actionid)->Delete();");
        StringAssert.Contains(source, "$rule->Delete();");
        StringAssert.Contains(source, "if ($savetype == \"criteria\" || $savetype == \"action\")");
        StringAssert.Contains(source, "die;");

        foreach (var assignment in new[]
        {
            "$criteria->UsePredefined = hmailGetPostVar(\"UsePredefined\", 0);",
            "$criteria->PredefinedField = hmailGetPostVar(\"PredefinedField\", 0);",
            "$criteria->MatchType = hmailGetPostVar(\"MatchType\", 0);",
            "$criteria->MatchValue = hmailGetPostVar(\"MatchValue\", 0);",
            "$criteria->HeaderField = hmailGetPostVar(\"HeaderField\", 0);",
            "$actionObj->Type = $type;",
            "$actionObj->To = hmailGetPostVar(\"To\", \"\");",
            "$actionObj->IMAPFolder = hmailGetPostVar(\"IMAPFolder\", \"\");",
            "$actionObj->ScriptFunction = hmailGetPostVar(\"ScriptFunction\", \"\");",
            "$actionObj->FromName = hmailGetPostVar(\"FromName\", \"\");",
            "$actionObj->FromAddress = hmailGetPostVar(\"FromAddress\", \"\");",
            "$actionObj->Subject = hmailGetPostVar(\"Subject\", \"\");",
            "$actionObj->Body = hmailGetPostVar(\"Body\", \"\");",
            "$actionObj->HeaderName = hmailGetPostVar(\"HeaderName\", \"\");",
            "$actionObj->AbortSpamFlagged = $forwardabortspamflagged == 1;",
            "$actionObj->AbortSpamFlagged = $replyabortspamflagged == 1;",
            "$rule->Name = hmailGetPostVar(\"Name\", \"\");",
            "$rule->Active = hmailGetPostVar(\"Active\", \"\") == \"1\";",
            "$rule->UseAND = hmailGetPostVar(\"UseAND\", \"\") == \"1\";"
        })
        {
            StringAssert.Contains(source, assignment);
        }

        StringAssert.Contains(source, "if (hmailGetAdminLevel() != ADMIN_SERVER)");
        StringAssert.Contains(source, "if ($type != eRADeleteEmail");
        StringAssert.Contains(source, "$actionObj->Value = hmailGetPostVar(\"Value\", \"\");");
        StringAssert.Contains(source, "$actionObj->Value = hmailGetPostVar(\"BindToAddress\", \"\");");
        StringAssert.Contains(source, "$criteria->Save();");
        StringAssert.Contains(source, "$actionObj->Save();");
        StringAssert.Contains(source, "$rule->Save();");
        StringAssert.Contains(source, "header(\"Location: $rule_link\");");
        StringAssert.Contains(source, "header(\"Location: index.php?page=rules\");");
        StringAssert.Contains(
            source,
            "header(\"Location: index.php?page=account&action=edit&accountid=$accountid&domainid=$domainid\");");
        StringAssert.Contains(
            source,
            "header(\"Location: index.php?page=rule&action=edit&domainid=$domainid&accountid=$accountid&ruleid=$ruleid\");");

        var ruleForm = ReadWebAdminFile("hm_rule.php");
        StringAssert.Contains(ruleForm, "method=\"post\"");
        StringAssert.Contains(ruleForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(ruleForm, "PrintHidden(\"page\", \"background_rule_save\")");
        StringAssert.Contains(ruleForm, "PrintHidden(\"savetype\", \"rule\")");
        StringAssert.Contains(ruleForm, "PrintHidden(\"action\", $action)");
        StringAssert.Contains(ruleForm, "PrintHidden(\"domainid\", $domainid)");
        StringAssert.Contains(ruleForm, "PrintHidden(\"accountid\", $accountid)");
        StringAssert.Contains(ruleForm, "PrintHidden(\"ruleid\", $ruleid)");
        StringAssert.Contains(ruleForm, "PrintHidden(\"savetype\", \"criteria\")");
        StringAssert.Contains(ruleForm, "PrintHidden(\"savetype\", \"action\")");

        var rulesForm = ReadWebAdminFile("hm_rules.php");
        Assert.IsTrue(
            rulesForm.Contains("method=\"post\"", StringComparison.Ordinal) ||
            rulesForm.Contains("method=\\\"post\\\"", StringComparison.Ordinal),
            "The global rule delete form must submit with POST.");
        StringAssert.Contains(rulesForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(rulesForm, "PrintHidden(\"page\", \"background_rule_save\")");
        StringAssert.Contains(rulesForm, "PrintHidden(\"savetype\", \"rule\")");
        StringAssert.Contains(rulesForm, "PrintHidden(\"action\", \"delete\")");

        var criteriaForm = ReadWebAdminFile("hm_rule_criteria.php");
        StringAssert.Contains(criteriaForm, "method=\"post\"");
        StringAssert.Contains(criteriaForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(criteriaForm, "PrintHidden(\"page\", \"background_rule_save\")");
        StringAssert.Contains(criteriaForm, "PrintHidden(\"savetype\", \"criteria\")");
        StringAssert.Contains(criteriaForm, "PrintHidden(\"criteriaid\", $criteriaid)");

        var actionForm = ReadWebAdminFile("hm_rule_action.php");
        StringAssert.Contains(actionForm, "method=\"post\"");
        StringAssert.Contains(actionForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(actionForm, "PrintHidden(\"page\", \"background_rule_save\")");
        StringAssert.Contains(actionForm, "PrintHidden(\"savetype\", \"action\")");
        StringAssert.Contains(actionForm, "PrintHidden(\"actionid\", $actionid)");

        var accountForm = ReadWebAdminFile("hm_account.php");
        StringAssert.Contains(accountForm, "PrintHidden(\"page\", \"background_rule_save\")");
        StringAssert.Contains(accountForm, "PrintHidden(\"savetype\", \"rule\")");
        StringAssert.Contains(accountForm, "PrintHiddenCsrfToken();");
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
