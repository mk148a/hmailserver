using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminSmtpAntispamPostOnlySourceTests
{
    [TestMethod]
    public void SmtpAntispamSaveUsesPostBodyAndRequiresPostCsrf()
    {
        var source = ReadSmtpAntispamPage();
        var saveStart = source.IndexOf("if($action == \"save\")", StringComparison.Ordinal);
        var renderStart = source.IndexOf("$SpamMarkThreshold =", saveStart, StringComparison.Ordinal);

        Assert.IsTrue(saveStart >= 0, "The SMTP AntiSpam save branch was not found.");
        Assert.IsTrue(renderStart > saveStart, "The SMTP AntiSpam save branch boundary was not found.");

        var saveBranch = source.Substring(saveStart, renderStart - saveStart);
        var csrfPosition = saveBranch.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstMutationPosition = saveBranch.IndexOf("$antiSpamSettings->", StringComparison.Ordinal);

        StringAssert.Contains(source, "hmailGetPostVar(\"action\"");
        StringAssert.Contains(source, "method=\"post\"");
        StringAssert.Contains(source, "PrintHiddenCsrfToken();");
        Assert.IsTrue(csrfPosition >= 0, "The SMTP AntiSpam save branch must require a POST CSRF token.");
        Assert.IsTrue(firstMutationPosition > csrfPosition, "AntiSpam settings must be written after CSRF validation.");
        Assert.IsFalse(
            saveBranch.Contains("hmailGetVar(", StringComparison.Ordinal),
            "SMTP AntiSpam mutation inputs must not use the mixed GET/POST accessor.");

        foreach (var name in new[]
        {
            "SpamMarkThreshold",
            "SpamDeleteThreshold",
            "SpamAssassinEnabled",
            "SpamAssassinHost",
            "SpamAssassinPort",
            "SpamAssassinMergeScore",
            "SpamAssassinScore",
            "usespf",
            "usespfscore",
            "usemxchecks",
            "usemxchecksscore",
            "checkhostinhelo",
            "checkhostinheloscore",
            "checkptr",
            "checkptrscore",
            "AddHeaderSpam",
            "AddHeaderReason",
            "PrependSubject",
            "PrependSubjectText",
            "MaximumMessageSize",
            "DKIMVerificationEnabled",
            "DKIMVerificationFailureScore"
        })
        {
            StringAssert.Contains(saveBranch, $"hmailGetPostVar(\"{name}\"");
        }

        foreach (var property in new[]
        {
            "SpamMarkThreshold",
            "SpamDeleteThreshold",
            "SpamAssassinEnabled",
            "SpamAssassinHost",
            "SpamAssassinPort",
            "SpamAssassinMergeScore",
            "SpamAssassinScore",
            "UseSPF",
            "UseSPFScore",
            "UseMXChecks",
            "UseMXChecksScore",
            "CheckHostInHelo",
            "CheckHostInHeloScore",
            "CheckPTR",
            "CheckPTRScore",
            "AddHeaderSpam",
            "AddHeaderReason",
            "PrependSubject",
            "PrependSubjectText",
            "MaximumMessageSize",
            "DKIMVerificationEnabled",
            "DKIMVerificationFailureScore"
        })
        {
            StringAssert.Contains(saveBranch, $"$antiSpamSettings->{property}");
        }
    }

    private static string ReadSmtpAntispamPage()
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
                "hm_smtp_antispam.php");

            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail("Could not locate hmailserver/source/WebAdmin/hm_smtp_antispam.php from the test output directory.");
        return string.Empty;
    }
}
