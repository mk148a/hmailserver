using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminServerMessagePostOnlySourceTests
{
    [TestMethod]
    public void ServerMessageSaveUsesPostBodyAndRequiresPostCsrf()
    {
        var saveSource = ReadWebAdminSource("background_servermessage_save.php");
        var formSource = ReadWebAdminSource("hm_servermessage.php");
        var csrfPosition = saveSource.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var firstInputPosition = saveSource.IndexOf("hmailGetPostVar(\"messageid\"", StringComparison.Ordinal);

        Assert.IsTrue(csrfPosition >= 0, "The server-message save handler must require a POST CSRF token.");
        Assert.IsTrue(firstInputPosition > csrfPosition, "Server-message mutation inputs must be read after CSRF validation.");
        Assert.IsFalse(saveSource.Contains("hmailGetVar(", StringComparison.Ordinal));
        StringAssert.Contains(saveSource, "hmailGetPostVar(\"messageid\"");
        StringAssert.Contains(saveSource, "hmailGetPostVar(\"messagename\"");
        StringAssert.Contains(saveSource, "hmailGetPostVar(\"messagetext\"");
        StringAssert.Contains(saveSource, "$obServerMessage->Text = $messagetext;");
        StringAssert.Contains(saveSource, "$obServerMessage->Save();");

        StringAssert.Contains(formSource, "method=\"post\"");
        StringAssert.Contains(formSource, "PrintHiddenCsrfToken();");
        StringAssert.Contains(formSource, "PrintHidden(\"page\", \"background_servermessage_save\")");
        StringAssert.Contains(formSource, "PrintHidden(\"messageid\", \"$messageid\")");
        StringAssert.Contains(formSource, "PrintHidden(\"messagename\", \"$messagename\")");
        StringAssert.Contains(formSource, "name=\"messagetext\"");
    }

    private static string ReadWebAdminSource(string fileName)
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
                fileName);

            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        Assert.Fail($"Could not locate hmailserver/source/WebAdmin/{fileName} from the test output directory.");
        return string.Empty;
    }
}
