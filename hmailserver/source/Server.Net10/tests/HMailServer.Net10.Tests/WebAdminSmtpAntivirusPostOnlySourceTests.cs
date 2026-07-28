using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminSmtpAntivirusPostOnlySourceTests
{
    [TestMethod]
    public void SmtpAntivirusSaveUsesPostBodyAndRequiresPostCsrf()
    {
        var source = ReadSmtpAntivirusPage();
        var saveStart = source.IndexOf("if($action == \"save\")", StringComparison.Ordinal);
        var renderStart = source.IndexOf("$avaction =", saveStart, StringComparison.Ordinal);

        Assert.IsTrue(saveStart >= 0, "The SMTP AntiVirus save branch was not found.");
        Assert.IsTrue(renderStart > saveStart, "The SMTP AntiVirus save branch boundary was not found.");

        var saveBranch = source.Substring(saveStart, renderStart - saveStart);
        var csrfPosition = saveBranch.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstMutationPosition = saveBranch.IndexOf("$obAntivirus->", StringComparison.Ordinal);

        StringAssert.Contains(source, "hmailGetPostVar(\"action\"");
        StringAssert.Contains(source, "method=\"post\"");
        StringAssert.Contains(source, "PrintHiddenCsrfToken();");
        Assert.IsTrue(csrfPosition >= 0, "The SMTP AntiVirus save branch must require a POST CSRF token.");
        Assert.IsTrue(firstMutationPosition > csrfPosition, "AntiVirus settings must be written after CSRF validation.");
        Assert.IsFalse(
            saveBranch.Contains("hmailGetVar(", StringComparison.Ordinal),
            "SMTP AntiVirus mutation inputs must not use the mixed GET/POST accessor.");

        foreach (var name in new[]
        {
            "avaction",
            "avnotifysender",
            "avnotifyreceiver",
            "MaximumMessageSize",
            "clamwinenabled",
            "clamwinexecutable",
            "clamwindbfolder",
            "ClamAVEnabled",
            "ClamAVHost",
            "ClamAVPort",
            "customscannerenabled",
            "customscannerexecutable",
            "customscannerreturnvalue",
            "EnableAttachmentBlocking"
        })
        {
            StringAssert.Contains(saveBranch, $"hmailGetPostVar(\"{name}\"");
        }

        foreach (var property in new[]
        {
            "Action",
            "NotifySender",
            "NotifyReceiver",
            "MaximumMessageSize",
            "ClamWinEnabled",
            "ClamWinExecutable",
            "ClamWinDBFolder",
            "ClamAVEnabled",
            "ClamAVHost",
            "ClamAVPort",
            "CustomScannerEnabled",
            "CustomScannerExecutable",
            "CustomScannerReturnValue",
            "EnableAttachmentBlocking"
        })
        {
            StringAssert.Contains(saveBranch, $"$obAntivirus->{property}");
        }
    }

    private static string ReadSmtpAntivirusPage()
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
                "hm_smtp_antivirus.php");

            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail("Could not locate hmailserver/source/WebAdmin/hm_smtp_antivirus.php from the test output directory.");
        return string.Empty;
    }
}
