using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminSecurityRangePostOnlySourceTests
{
    [TestMethod]
    public void SecurityRangeMutationUsesPostBodyAndRequiresPostCsrf()
    {
        var source = ReadWebAdminFile("background_securityrange_save.php");
        var authPosition = source.IndexOf(
            "if (hmailGetAdminLevel() != ADMIN_SERVER)",
            StringComparison.Ordinal);
        var csrfPosition = source.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstPostReadPosition = source.IndexOf(
            "hmailGetPostVar(\"action\"",
            StringComparison.Ordinal);

        Assert.IsTrue(authPosition >= 0, "The server-admin denial guard was not found.");
        Assert.IsTrue(csrfPosition > authPosition, "CSRF validation must follow the server-admin denial guard.");
        Assert.IsTrue(firstPostReadPosition > csrfPosition, "Request values must be read after CSRF validation.");
        Assert.IsFalse(source.Contains("hmailGetVar(", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("hmailRequirePost();", StringComparison.Ordinal));

        StringAssert.Contains(source, "hmailHackingAttemp();");

        foreach (var field in new[]
        {
            "action",
            "securityrangeid",
            "securityrangename",
            "securityrangepriority",
            "securityrangelowerip",
            "securityrangeupperip",
            "allowsmtpconnections",
            "allowpop3connections",
            "allowimapconnections",
            "allowlocaltolocal",
            "allowlocaltoremote",
            "allowremotetolocal",
            "allowremotetoremote",
            "enablespamprotection",
            "EnableAntiVirus",
            "IsForwardingRelay",
            "RequireSSLTLSForAuth",
            "Expires",
            "ExpiresTime",
            "RequireSMTPAuthLocalToLocal",
            "RequireSMTPAuthLocalToExternal",
            "RequireSMTPAuthExternalToLocal",
            "RequireSMTPAuthExternalToExternal"
        })
        {
            StringAssert.Contains(source, $"hmailGetPostVar(\"{field}\"");
        }

        foreach (var accessor in new[]
        {
            "hmailGetPostVar(\"action\",\"\")",
            "hmailGetPostVar(\"securityrangeid\",\"\")",
            "hmailGetPostVar(\"securityrangename\",\"\")",
            "hmailGetPostVar(\"securityrangepriority\",\"0\")",
            "hmailGetPostVar(\"securityrangelowerip\",\"0\")",
            "hmailGetPostVar(\"securityrangeupperip\",\"0\")",
            "hmailGetPostVar(\"allowsmtpconnections\",\"0\")",
            "hmailGetPostVar(\"allowpop3connections\",\"0\")",
            "hmailGetPostVar(\"allowimapconnections\",\"0\")",
            "hmailGetPostVar(\"allowlocaltolocal\",\"0\")",
            "hmailGetPostVar(\"allowlocaltoremote\",\"0\")",
            "hmailGetPostVar(\"allowremotetolocal\",\"0\")",
            "hmailGetPostVar(\"allowremotetoremote\",\"0\")",
            "hmailGetPostVar(\"enablespamprotection\",\"0\")",
            "hmailGetPostVar(\"EnableAntiVirus\",\"0\")",
            "hmailGetPostVar(\"IsForwardingRelay\",\"0\")",
            "hmailGetPostVar(\"RequireSSLTLSForAuth\",\"0\")",
            "hmailGetPostVar(\"Expires\",0)",
            "hmailGetPostVar(\"ExpiresTime\",\"\")",
            "hmailGetPostVar(\"RequireSMTPAuthLocalToLocal\", 0)",
            "hmailGetPostVar(\"RequireSMTPAuthLocalToExternal\", 0)",
            "hmailGetPostVar(\"RequireSMTPAuthExternalToLocal\", 0)",
            "hmailGetPostVar(\"RequireSMTPAuthExternalToExternal\", 0)"
        })
        {
            StringAssert.Contains(source, accessor);
        }

        StringAssert.Contains(source, "if ($action == \"edit\")");
        StringAssert.Contains(source, "$obBaseApp->Settings->SecurityRanges->ItemByDBID($securityrangeid)");
        StringAssert.Contains(source, "elseif ($action == \"add\")");
        StringAssert.Contains(source, "$obBaseApp->Settings->SecurityRanges->Add()");
        StringAssert.Contains(source, "elseif ($action == \"delete\")");
        StringAssert.Contains(source, "$obBaseApp->Settings->SecurityRanges->DeleteByDBID($securityrangeid);");

        foreach (var assignment in new[]
        {
            "$obSecurityRange->Name = $securityrangename;",
            "$obSecurityRange->Priority = $securityrangepriority;",
            "$obSecurityRange->LowerIP = $securityrangelowerip;",
            "$obSecurityRange->UpperIP = $securityrangeupperip;",
            "$obSecurityRange->AllowSMTPConnections = $allowsmtpconnections;",
            "$obSecurityRange->AllowPOP3Connections = $allowpop3connections;",
            "$obSecurityRange->AllowIMAPConnections = $allowimapconnections;",
            "$obSecurityRange->AllowDeliveryFromLocalToLocal = $allowlocaltolocal;",
            "$obSecurityRange->AllowDeliveryFromLocalToRemote = $allowlocaltoremote;",
            "$obSecurityRange->AllowDeliveryFromRemoteToLocal = $allowremotetolocal;",
            "$obSecurityRange->AllowDeliveryFromRemoteToRemote = $allowremotetoremote;",
            "$obSecurityRange->EnableSpamProtection = $enablespamprotection;",
            "$obSecurityRange->EnableAntiVirus = $EnableAntiVirus;",
            "$obSecurityRange->IsForwardingRelay = $IsForwardingRelay;",
            "$obSecurityRange->RequireSSLTLSForAuth = $RequireSSLTLSForAuth;",
            "$obSecurityRange->Expires = $Expires;",
            "$obSecurityRange->ExpiresTime = $ExpiresTime;"
        })
        {
            StringAssert.Contains(source, assignment);
        }

        foreach (var field in new[]
        {
            "RequireSMTPAuthLocalToLocal",
            "RequireSMTPAuthLocalToExternal",
            "RequireSMTPAuthExternalToLocal",
            "RequireSMTPAuthExternalToExternal"
        })
        {
            StringAssert.Contains(
                source,
                $"$obSecurityRange->{field} = hmailGetPostVar(\"{field}\", 0);");
        }

        StringAssert.Contains(source, "$obSecurityRange->Save();");
        StringAssert.Contains(source, "$securityrangeid = $obSecurityRange->ID;");
        StringAssert.Contains(source, "header(\"Location: index.php?page=securityranges\");");
        StringAssert.Contains(
            source,
            "header(\"Location: index.php?page=securityrange&action=edit&securityrangeid=$securityrangeid\");");

        var editForm = ReadWebAdminFile("hm_securityrange.php");
        StringAssert.Contains(editForm, "method=\"post\"");
        StringAssert.Contains(editForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(editForm, "PrintHidden(\"page\", \"background_securityrange_save\")");
        StringAssert.Contains(editForm, "PrintHidden(\"action\", $action)");
        StringAssert.Contains(editForm, "PrintHidden(\"securityrangeid\", $securityrangeid)");

        var collectionForm = ReadWebAdminFile("hm_securityranges.php");
        Assert.IsTrue(
            collectionForm.Contains("method=\"post\"", StringComparison.Ordinal) ||
            collectionForm.Contains("method=\\\"post\\\"", StringComparison.Ordinal),
            "The security-range delete form must submit with POST.");
        StringAssert.Contains(collectionForm, "PrintHiddenCsrfToken();");
        StringAssert.Contains(collectionForm, "PrintHidden(\"page\", \"background_securityrange_save\")");
        StringAssert.Contains(collectionForm, "PrintHidden(\"action\", \"delete\")");
        StringAssert.Contains(collectionForm, "PrintHidden(\"securityrangeid\", $securityrangeid)");
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
