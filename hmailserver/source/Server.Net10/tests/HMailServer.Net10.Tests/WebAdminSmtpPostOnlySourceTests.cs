using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminSmtpPostOnlySourceTests
{
    [TestMethod]
    public void SmtpSaveUsesPostBodyAndRequiresPostCsrf()
    {
        var source = ReadSmtpPage();
        var saveStart = source.IndexOf("if($action == \"save\")", StringComparison.Ordinal);
        var renderStart = source.IndexOf("$maxsmtpconnections =", saveStart, StringComparison.Ordinal);

        Assert.IsTrue(saveStart >= 0, "The SMTP save branch was not found.");
        Assert.IsTrue(renderStart > saveStart, "The SMTP save branch boundary was not found.");

        var saveBranch = source.Substring(saveStart, renderStart - saveStart);
        var csrfPosition = saveBranch.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstMutationPosition = saveBranch.IndexOf("$obSettings->", StringComparison.Ordinal);

        StringAssert.Contains(source, "hmailGetPostVar(\"action\"");
        StringAssert.Contains(source, "method=\"post\"");
        StringAssert.Contains(source, "PrintHiddenCsrfToken();");
        Assert.IsTrue(csrfPosition >= 0, "The SMTP save branch must require a POST CSRF token.");
        Assert.IsTrue(firstMutationPosition > csrfPosition, "SMTP settings must be written after CSRF validation.");
        Assert.IsFalse(
            saveBranch.Contains("hmailGetVar(", StringComparison.Ordinal),
            "SMTP mutation inputs must not use the mixed GET/POST accessor.");

        foreach (var name in new[]
        {
            "maxsmtpconnections",
            "welcomesmtp",
            "smtpnooftries",
            "smtpminutesbetweentry",
            "HostName",
            "smtprelayer",
            "smtprelayerport",
            "SMTPRelayerRequiresAuthentication",
            "SMTPRelayerUsername",
            "SMTPRelayerConnectionSecurity",
            "SMTPRelayerPassword",
            "smtprulelooplimit",
            "maxmessagesize",
            "smtpdeliverybindtoip",
            "maxsmtprecipientsinbatch",
            "AllowSMTPAuthPlain",
            "AllowMailFromNull",
            "AllowIncorrectLineEndings",
            "DisconnectInvalidClients",
            "MaxNumberOfInvalidCommands",
            "AddDeliveredToHeader",
            "MaxNumberOfMXHosts",
            "SMTPConnectionSecurity"
        })
        {
            StringAssert.Contains(saveBranch, $"hmailGetPostVar(\"{name}\"");
        }

        foreach (var property in new[]
        {
            "MaxSMTPConnections",
            "WelcomeSMTP",
            "SMTPNoOfTries",
            "SMTPMinutesBetweenTry",
            "HostName",
            "SMTPRelayer",
            "SMTPRelayerPort",
            "SMTPRelayerRequiresAuthentication",
            "SMTPRelayerUsername",
            "SMTPRelayerConnectionSecurity",
            "RuleLoopLimit",
            "MaxMessageSize",
            "SMTPDeliveryBindToIP",
            "MaxSMTPRecipientsInBatch",
            "AllowSMTPAuthPlain",
            "DenyMailFromNull",
            "AllowIncorrectLineEndings",
            "DisconnectInvalidClients",
            "MaxNumberOfInvalidCommands",
            "AddDeliveredToHeader",
            "MaxNumberOfMXHosts",
            "SMTPConnectionSecurity"
        })
        {
            StringAssert.Contains(saveBranch, $"$obSettings->{property}");
        }

        StringAssert.Contains(saveBranch, "$obSettings->SetSMTPRelayerPassword");
        StringAssert.Contains(saveBranch, "if (hmailGetPostVar(\"SMTPRelayerPassword\",\"\") != \"\")");
    }

    private static string ReadSmtpPage()
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
                "hm_smtp.php");

            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail("Could not locate hmailserver/source/WebAdmin/hm_smtp.php from the test output directory.");
        return string.Empty;
    }
}
