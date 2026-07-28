using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminRoutePostOnlySourceTests
{
    [TestMethod]
    public void RouteMutationUsesPostBodyAndRequiresPostCsrf()
    {
        var source = ReadWebAdminFile("background_route_save.php");
        var authPosition = source.IndexOf("if (hmailGetAdminLevel() != 2)", StringComparison.Ordinal);
        var csrfPosition = source.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstPostReadPosition = source.IndexOf("hmailGetPostVar(\"action\"", StringComparison.Ordinal);

        Assert.IsTrue(authPosition >= 0, "The server-admin denial guard was not found.");
        Assert.IsTrue(csrfPosition > authPosition, "CSRF validation must follow the server-admin denial guard.");
        Assert.IsTrue(firstPostReadPosition > csrfPosition, "Request values must be read after CSRF validation.");
        Assert.IsFalse(source.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("hmailRequirePost();", StringComparison.Ordinal));

        StringAssert.Contains(source, "hmailHackingAttemp();");

        foreach (var field in new[]
        {
            "action",
            "routeid",
            "routedomainname",
            "routetargetsmtphost",
            "routetargetsmtpport",
            "TreatSenderAsLocalDomain",
            "TreatRecipientAsLocalDomain",
            "routenumberoftries",
            "routemminutesbetweentry",
            "routerequiresauth",
            "routeauthusername",
            "routeauthpassword",
            "ConnectionSecurity",
            "AllAddresses"
        })
        {
            StringAssert.Contains(source, $"hmailGetPostVar(\"{field}\"");
        }

        foreach (var accessor in new[]
        {
            "hmailGetPostVar(\"action\",\"\")",
            "hmailGetPostVar(\"routeid\",\"\")",
            "hmailGetPostVar(\"routedomainname\",\"\")",
            "hmailGetPostVar(\"routetargetsmtphost\",\"0\")",
            "hmailGetPostVar(\"routetargetsmtpport\",\"0\")",
            "hmailGetPostVar(\"TreatSenderAsLocalDomain\",\"0\")",
            "hmailGetPostVar(\"TreatRecipientAsLocalDomain\",\"0\")",
            "hmailGetPostVar(\"routenumberoftries\",\"0\")",
            "hmailGetPostVar(\"routemminutesbetweentry\",\"0\")",
            "hmailGetPostVar(\"routerequiresauth\",\"0\")",
            "hmailGetPostVar(\"routeauthusername\",\"0\")",
            "hmailGetPostVar(\"routeauthpassword\",\"0\")",
            "hmailGetPostVar(\"ConnectionSecurity\",\"0\")",
            "hmailGetPostVar(\"AllAddresses\",\"0\")"
        })
        {
            StringAssert.Contains(source, accessor);
        }

        StringAssert.Contains(source, "if ($action == \"edit\")");
        StringAssert.Contains(source, "$obBaseApp->Settings->Routes->ItemByDBID($routeid)");
        StringAssert.Contains(source, "elseif ($action == \"add\")");
        StringAssert.Contains(source, "$obBaseApp->Settings->Routes->Add()");
        StringAssert.Contains(source, "elseif ($action == \"delete\")");
        StringAssert.Contains(source, "$obBaseApp->Settings->Routes->DeleteByDBID($routeid);");
        StringAssert.Contains(source, "$obRoute->DomainName = $routedomainname;");
        StringAssert.Contains(source, "$obRoute->TargetSMTPHost = $routetargetsmtphost;");
        StringAssert.Contains(source, "$obRoute->TargetSMTPPort = $routetargetsmtpport;");
        StringAssert.Contains(source, "$obRoute->TreatSenderAsLocalDomain = $TreatSenderAsLocalDomain;");
        StringAssert.Contains(source, "$obRoute->TreatRecipientAsLocalDomain = $TreatRecipientAsLocalDomain;");
        StringAssert.Contains(source, "$obRoute->NumberOfTries = $routenumberoftries;");
        StringAssert.Contains(source, "$obRoute->MinutesBetweenTry = $routemminutesbetweentry;");
        StringAssert.Contains(source, "$obRoute->RelayerRequiresAuth = $routerequiresauth;");
        StringAssert.Contains(source, "$obRoute->RelayerAuthUsername = $routeauthusername;");
        StringAssert.Contains(source, "$obRoute->AllAddresses = hmailGetPostVar(\"AllAddresses\",\"0\");");
        StringAssert.Contains(source, "$obRoute->ConnectionSecurity = $ConnectionSecurity;");
        StringAssert.Contains(source, "if ($routeauthpassword != \"\")");
        StringAssert.Contains(source, "$obRoute->SetRelayerAuthPassword($routeauthpassword);");
        StringAssert.Contains(source, "$obRoute->Save();");
        StringAssert.Contains(source, "header(\"Location: index.php?page=routes\");");
        StringAssert.Contains(source, "header(\"Location: index.php?page=route&action=edit&routeid=$routeid\");");

        var routeForm = ReadWebAdminFile("hm_route.php");
        StringAssert.Contains(routeForm, "method=\"post\"");
        StringAssert.Contains(routeForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(routeForm, "PrintHidden(\"page\", \"background_route_save\")");
        StringAssert.Contains(routeForm, "PrintHidden(\"action\", $action)");
        StringAssert.Contains(routeForm, "PrintHidden(\"routeid\", $routeid)");

        var routesForm = ReadWebAdminFile("hm_routes.php");
        Assert.IsTrue(
            routesForm.Contains("method=\"post\"", StringComparison.Ordinal) ||
            routesForm.Contains("method=\\\"post\\\"", StringComparison.Ordinal),
            "The route delete form must submit with POST.");
        StringAssert.Contains(routesForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(routesForm, "PrintHidden(\"page\", \"background_route_save\")");
        StringAssert.Contains(routesForm, "PrintHidden(\"action\", \"delete\")");
        StringAssert.Contains(routesForm, "PrintHidden(\"routeid\", $routeid)");
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
