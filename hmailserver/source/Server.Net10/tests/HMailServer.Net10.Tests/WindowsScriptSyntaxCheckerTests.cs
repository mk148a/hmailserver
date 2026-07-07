using System.Text;
using HMailServer.Scripting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WindowsScriptSyntaxCheckerTests
{
    [TestMethod]
    public void CheckSyntax_MissingOrEmptyFileReturnsLegacyEmptySuccess()
    {
        var checker = CreateChecker();
        var directory = CreateTemporaryDirectory();
        try
        {
            var missingFile = Path.Combine(directory, "missing.vbs");
            var emptyFile = Path.Combine(directory, "empty.vbs");
            File.WriteAllText(emptyFile, string.Empty, Encoding.Unicode);

            Assert.AreEqual(string.Empty, checker.CheckSyntax("VBScript", missingFile));
            Assert.AreEqual(string.Empty, checker.CheckSyntax("VBScript", emptyFile));
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void CheckSyntax_ValidVbScriptAndJScriptReturnEmptySuccess()
    {
        var checker = CreateChecker();
        var directory = CreateTemporaryDirectory();
        try
        {
            var vbScript = Path.Combine(directory, "EventHandlers.vbs");
            var jScript = Path.Combine(directory, "EventHandlers.js");
            File.WriteAllText(vbScript, "Sub OnClientConnect(oClient)\r\nEnd Sub\r\n", Encoding.Unicode);
            File.WriteAllText(jScript, "function OnClientConnect(oClient) { return; }\r\n", Encoding.Unicode);

            Assert.AreEqual(string.Empty, checker.CheckSyntax("VBScript", vbScript));
            Assert.AreEqual(string.Empty, checker.CheckSyntax("JScript", jScript));
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void CheckSyntax_InvalidScriptReturnsFileScopedErrorText()
    {
        var checker = CreateChecker();
        var directory = CreateTemporaryDirectory();
        try
        {
            var scriptFile = Path.Combine(directory, "EventHandlers.vbs");
            File.WriteAllText(scriptFile, "Sub Broken(\r\nEnd Sub\r\n", Encoding.Unicode);

            var result = checker.CheckSyntax("VBScript", scriptFile);

            StringAssert.StartsWith(result, $"File: {scriptFile}\r\n");
            Assert.IsGreaterThan(scriptFile.Length, result.Length);
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void CheckSyntax_NonTerminatingTopLevelCodeReturnsBoundedTimeoutError()
    {
        var checker = CreateChecker(TimeSpan.FromMilliseconds(250));
        var directory = CreateTemporaryDirectory();
        try
        {
            var scriptFile = Path.Combine(directory, "EventHandlers.vbs");
            File.WriteAllText(scriptFile, "Do While True\r\nLoop\r\n", Encoding.Unicode);

            var result = checker.CheckSyntax("VBScript", scriptFile);

            Assert.AreEqual($"File: {scriptFile}\r\nScript syntax check timed out.", result);
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    private static WindowsScriptSyntaxChecker CreateChecker(TimeSpan? timeout = null) =>
        new(new WindowsScriptRuleExecutorOptions
        {
            CScriptPath = GetCscriptPathOrInconclusive(),
            Timeout = timeout ?? TimeSpan.FromSeconds(5)
        });

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "hmailserver syntax " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string GetCscriptPathOrInconclusive()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows Script Host is only available on Windows.");
        }

        var path = Path.Combine(Environment.SystemDirectory, "cscript.exe");
        if (!File.Exists(path))
        {
            Assert.Inconclusive("cscript.exe is not available on this machine.");
        }

        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 9)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException) when (attempt < 9)
            {
                Thread.Sleep(50);
            }
        }
    }
}
