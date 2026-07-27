using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminBackupPostOnlySourceTests
{
    [TestMethod]
    public void BackupMutationsUsePostBodyAndRequirePostCsrf()
    {
        var source = ReadBackupPage();
        var saveStart = source.IndexOf("if($action == \"save\")", StringComparison.Ordinal);
        var startBackupStart = source.IndexOf(
            "elseif ($action == \"startbackup\")",
            saveStart,
            StringComparison.Ordinal);

        Assert.IsTrue(saveStart >= 0, "The backup save branch was not found.");
        Assert.IsTrue(startBackupStart > saveStart, "The backup start branch was not found.");

        var saveBranch = source.Substring(saveStart, startBackupStart - saveStart);
        var startBackupBranch = source.Substring(startBackupStart);

        StringAssert.Contains(source, "$action\t   = hmailGetPostVar(\"action\",\"\");");
        StringAssert.Contains(saveBranch, "hmailRequirePostCsrfToken();");
        StringAssert.Contains(startBackupBranch, "hmailRequirePostCsrfToken();");
        StringAssert.Contains(startBackupBranch, "$obBaseApp->BackupManager->StartBackup();");

        foreach (var name in new[]
        {
            "backupdestination",
            "backupsettings",
            "backupdomains",
            "backupmessages",
            "backupcompress"
        })
        {
            StringAssert.Contains(saveBranch, $"hmailGetPostVar(\"{name}\"");
            Assert.IsFalse(
                saveBranch.Contains($"hmailGetVar(\"{name}\"", StringComparison.Ordinal),
                $"Mutation field {name} must not use the mixed GET/POST accessor.");
        }

        StringAssert.Contains(source, "method=\"post\"");
        StringAssert.Contains(source, "PrintHiddenCsrfToken();");
    }

    private static string ReadBackupPage()
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
                "hm_backup.php");

            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail("Could not locate hmailserver/source/WebAdmin/hm_backup.php from the test output directory.");
        return string.Empty;
    }
}
