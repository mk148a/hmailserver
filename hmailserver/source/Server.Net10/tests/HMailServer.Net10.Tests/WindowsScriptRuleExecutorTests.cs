using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Scripting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WindowsScriptRuleExecutorTests
{
    [TestMethod]
    public void Execute_RunsVbScriptFunctionAndReturnsMutatedMessage()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub Rule_Custom(obMessage)
   Dim fso, inputFile, messageText, outputFile
   Set fso = CreateObject("Scripting.FileSystemObject")
   Set inputFile = fso.OpenTextFile(obMessage.FileName, 1, False)
   messageText = inputFile.ReadAll
   inputFile.Close
   Set outputFile = fso.CreateTextFile(obMessage.FileName, True, False)
   outputFile.Write "X-Script-Rule: yes" & vbCrLf & messageText
   outputFile.Close
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            var result = executor.Execute(
                CreateRequest(
                    "Rule_Custom",
                    "Subject: Script\r\n\r\nBody\r\n"u8.ToArray()),
                CancellationToken.None);

            Assert.IsTrue(result.Accepted, result.FailureResponse);
            Assert.IsFalse(result.DropMessage);
            Assert.IsNotNull(result.MessageData);
            StringAssert.Contains(
                Encoding.ASCII.GetString(result.MessageData),
                "X-Script-Rule: yes\r\nSubject: Script");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_ReturnsDropOrRejectStateSetByVbScript()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub Rule_Drop(obMessage)
   obMessage.DropMessage = True
End Sub

Sub Rule_Reject(obMessage)
   obMessage.RejectReason = "550 blocked by script"
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            var drop = executor.Execute(
                CreateRequest(
                    "Rule_Drop",
                    "Subject: Drop\r\n\r\nBody\r\n"u8.ToArray()),
                CancellationToken.None);
            var reject = executor.Execute(
                CreateRequest(
                    "Rule_Reject",
                    "Subject: Reject\r\n\r\nBody\r\n"u8.ToArray()),
                CancellationToken.None);

            Assert.IsTrue(drop.Accepted, drop.FailureResponse);
            Assert.IsTrue(drop.DropMessage);
            Assert.IsFalse(reject.Accepted);
            Assert.AreEqual("550 blocked by script", reject.FailureResponse);
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    private static WindowsScriptRuleExecutor CreateExecutor(
        string eventDirectory,
        string cscriptPath) =>
        new(
            new WindowsScriptRuleExecutorOptions
            {
                Enabled = true,
                Language = "VBScript",
                EventDirectory = eventDirectory,
                Timeout = TimeSpan.FromSeconds(5),
                CScriptPath = cscriptPath
            });

    private static SmtpRuleScriptExecutionRequest CreateRequest(
        string functionName,
        byte[] messageData) =>
        new(
            functionName,
            RuleId: 1,
            RuleName: "rule",
            AccountId: 0,
            MailFrom: "sender@example.test",
            Recipients: [],
            messageData);

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

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "hmailserver-script-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
