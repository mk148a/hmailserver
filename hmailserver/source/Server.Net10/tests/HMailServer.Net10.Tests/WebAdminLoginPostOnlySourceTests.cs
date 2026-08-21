using System.Text;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WebAdminLoginPostOnlySourceTests
{
    [TestMethod]
    public void LoginHandlerRequiresPostCsrfBeforeCredentialReadsAndFormPostsBackgroundLogin()
    {
        var handler = ReadWebAdminFile("background_login.php");
        var csrfPosition = handler.IndexOf("hmailRequirePostCsrfToken();", StringComparison.Ordinal);
        var usernamePosition = handler.IndexOf("hmailGetPostVar(\"username\"", StringComparison.Ordinal);
        var passwordPosition = handler.IndexOf("hmailGetPostVar(\"password\"", StringComparison.Ordinal);

        Assert.IsTrue(csrfPosition >= 0, "The login handler must require POST CSRF validation.");
        Assert.IsTrue(usernamePosition > csrfPosition, "The username must be read after CSRF validation.");
        Assert.IsTrue(passwordPosition > csrfPosition, "The password must be read after CSRF validation.");
        Assert.IsFalse(handler.Contains("hmailGetVar(", StringComparison.Ordinal));

        var loginForm = ReadWebAdminFile("hm_login.php");
        StringAssert.Contains(loginForm, "method=\"post\"");
        StringAssert.Contains(loginForm, "PrintHidden(\"page\", \"background_login\")");
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
