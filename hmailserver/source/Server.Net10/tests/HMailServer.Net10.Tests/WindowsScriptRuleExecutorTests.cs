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
    public void Execute_ExposesLegacyMessageFacadeToVbScript()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub Rule_UpdateMessage(obMessage)
   If obMessage.Subject <> "Original" Then
      obMessage.RejectReason = "subject not loaded"
      Exit Sub
   End If
   If obMessage.From <> "Sender <sender@example.test>" Then
      obMessage.RejectReason = "from not loaded"
      Exit Sub
   End If
   If obMessage.To <> "dest@example.test" Then
      obMessage.RejectReason = "to not loaded"
      Exit Sub
   End If
   If obMessage.CC <> "copy@example.test" Then
      obMessage.RejectReason = "cc not loaded"
      Exit Sub
   End If
   If obMessage.HeaderValue("X-Folded") <> "one two" Then
      obMessage.RejectReason = "folded header not loaded"
      Exit Sub
   End If

   obMessage.Subject = "Changed"
   obMessage.From = "Updated <updated@example.test>"
   obMessage.To = "next@example.test"
   obMessage.CC = "copy2@example.test"
   obMessage.Body = "Changed body" & vbCrLf
   obMessage.HeaderValue("X-Legacy") = "yes"
   obMessage.Save
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            var result = executor.Execute(
                CreateRequest(
                    "Rule_UpdateMessage",
                    Encoding.ASCII.GetBytes(
                        "From: Sender <sender@example.test>\r\n" +
                        "To: dest@example.test\r\n" +
                        "CC: copy@example.test\r\n" +
                        "Subject: Original\r\n" +
                        "X-Folded: one\r\n two\r\n" +
                        "\r\n" +
                        "Original body\r\n")),
                CancellationToken.None);

            Assert.IsTrue(result.Accepted, result.FailureResponse);
            Assert.IsNotNull(result.MessageData);
            var messageText = Encoding.ASCII.GetString(result.MessageData);
            StringAssert.Contains(messageText, "Subject: Changed\r\n");
            StringAssert.Contains(messageText, "From: Updated <updated@example.test>\r\n");
            StringAssert.Contains(messageText, "To: next@example.test\r\n");
            StringAssert.Contains(messageText, "Cc: copy2@example.test\r\n");
            StringAssert.Contains(messageText, "X-Legacy: yes\r\n");
            StringAssert.Contains(messageText, "\r\n\r\nChanged body\r\n");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_ExposesLegacyMessageFacadeToJScript()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.js"),
                """
function Rule_UpdateMessage(obMessage) {
  if (obMessage.Subject !== "Original") {
    obMessage.RejectReason = "subject not loaded";
    return;
  }
  if (obMessage.From !== "Sender <sender@example.test>") {
    obMessage.RejectReason = "from not loaded";
    return;
  }
  if (obMessage.To !== "dest@example.test") {
    obMessage.RejectReason = "to not loaded";
    return;
  }
  if (obMessage.CC !== "copy@example.test") {
    obMessage.RejectReason = "cc not loaded";
    return;
  }
  if (obMessage.HeaderValue("X-Folded") !== "one two") {
    obMessage.RejectReason = "folded header not loaded";
    return;
  }

  obMessage.Subject = "Changed JS";
  obMessage.From = "JS Sender <js@example.test>";
  obMessage.To = "js-next@example.test";
  obMessage.CC = "js-copy@example.test";
  obMessage.Body = "Changed JS body\r\n";
  obMessage.SetHeaderValue("X-JScript", "yes");
  obMessage.Save();
}
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript, "JScript");

            var result = executor.Execute(
                CreateRequest(
                    "Rule_UpdateMessage",
                    Encoding.ASCII.GetBytes(
                        "From: Sender <sender@example.test>\r\n" +
                        "To: dest@example.test\r\n" +
                        "CC: copy@example.test\r\n" +
                        "Subject: Original\r\n" +
                        "X-Folded: one\r\n two\r\n" +
                        "\r\n" +
                        "Original body\r\n")),
                CancellationToken.None);

            Assert.IsTrue(result.Accepted, result.FailureResponse);
            Assert.IsNotNull(result.MessageData);
            var messageText = Encoding.ASCII.GetString(result.MessageData);
            StringAssert.Contains(messageText, "Subject: Changed JS\r\n");
            StringAssert.Contains(messageText, "From: JS Sender <js@example.test>\r\n");
            StringAssert.Contains(messageText, "To: js-next@example.test\r\n");
            StringAssert.Contains(messageText, "Cc: js-copy@example.test\r\n");
            StringAssert.Contains(messageText, "X-JScript: yes\r\n");
            StringAssert.Contains(messageText, "\r\n\r\nChanged JS body\r\n");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_ExposesRecipientCollectionToVbScript()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub Rule_UpdateRecipients(obMessage)
   If obMessage.FromAddress <> "sender@example.test" Then
      obMessage.RejectReason = "from address not loaded"
      Exit Sub
   End If
   If obMessage.Recipients.Count <> 2 Then
      obMessage.RejectReason = "recipient count not loaded"
      Exit Sub
   End If

   Dim firstRecipient, secondRecipient
   Set firstRecipient = obMessage.Recipients.Item(0)
   Set secondRecipient = obMessage.Recipients.Item(1)
   If firstRecipient.Address <> "local@example.test" Then
      obMessage.RejectReason = "first recipient not loaded"
      Exit Sub
   End If
   If Not firstRecipient.IsLocalUser Then
      obMessage.RejectReason = "local flag not loaded"
      Exit Sub
   End If
   If secondRecipient.OriginalAddress <> "Alias <alias@example.test>" Then
      obMessage.RejectReason = "original recipient not loaded"
      Exit Sub
   End If

   obMessage.ClearRecipients
   obMessage.AddRecipient "Added User", "added@example.test"
   If obMessage.Recipients.Count <> 1 Then
      obMessage.RejectReason = "recipient add failed"
      Exit Sub
   End If
   If obMessage.Recipients.Item(0).Address <> "added@example.test" Then
      obMessage.RejectReason = "added recipient not loaded"
      Exit Sub
   End If
   obMessage.Save
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            var result = executor.Execute(
                CreateRequest(
                    "Rule_UpdateRecipients",
                    "To: old@example.test\r\nCc: copy@example.test\r\nSubject: Recipients\r\n\r\nBody\r\n"u8.ToArray(),
                    CreateRecipients()),
                CancellationToken.None);

            Assert.IsTrue(result.Accepted, result.FailureResponse);
            Assert.IsNotNull(result.MessageData);
            var messageText = Encoding.ASCII.GetString(result.MessageData);
            StringAssert.Contains(messageText, "To: \"Added User\" <added@example.test>\r\n");
            Assert.IsFalse(messageText.Contains("Cc:", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_ExposesRecipientCollectionToJScript()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.js"),
                """
function Rule_UpdateRecipients(obMessage) {
  if (obMessage.FromAddress !== "sender@example.test") {
    obMessage.RejectReason = "from address not loaded";
    return;
  }
  if (obMessage.Recipients.Count !== 2) {
    obMessage.RejectReason = "recipient count not loaded";
    return;
  }

  var firstRecipient = obMessage.Recipients.Item(0);
  var secondRecipient = obMessage.Recipients.Item(1);
  if (firstRecipient.Address !== "local@example.test") {
    obMessage.RejectReason = "first recipient not loaded";
    return;
  }
  if (!firstRecipient.IsLocalUser) {
    obMessage.RejectReason = "local flag not loaded";
    return;
  }
  if (secondRecipient.OriginalAddress !== "Alias <alias@example.test>") {
    obMessage.RejectReason = "original recipient not loaded";
    return;
  }

  obMessage.ClearRecipients();
  obMessage.AddRecipient("Added JS", "added-js@example.test");
  if (obMessage.Recipients.Count !== 1) {
    obMessage.RejectReason = "recipient add failed";
    return;
  }
  if (obMessage.Recipients.Item(0).Address !== "added-js@example.test") {
    obMessage.RejectReason = "added recipient not loaded";
    return;
  }
  obMessage.Save();
}
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript, "JScript");

            var result = executor.Execute(
                CreateRequest(
                    "Rule_UpdateRecipients",
                    "To: old@example.test\r\nCc: copy@example.test\r\nSubject: Recipients\r\n\r\nBody\r\n"u8.ToArray(),
                    CreateRecipients()),
                CancellationToken.None);

            Assert.IsTrue(result.Accepted, result.FailureResponse);
            Assert.IsNotNull(result.MessageData);
            var messageText = Encoding.ASCII.GetString(result.MessageData);
            StringAssert.Contains(messageText, "To: \"Added JS\" <added-js@example.test>\r\n");
            Assert.IsFalse(messageText.Contains("Cc:", StringComparison.OrdinalIgnoreCase));
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
        string cscriptPath,
        string language = "VBScript") =>
        new(
            new WindowsScriptRuleExecutorOptions
            {
                Enabled = true,
                Language = language,
                EventDirectory = eventDirectory,
                Timeout = TimeSpan.FromSeconds(5),
                CScriptPath = cscriptPath
            });

    private static SmtpRuleScriptExecutionRequest CreateRequest(
        string functionName,
        byte[] messageData,
        IReadOnlyList<SmtpResolvedRecipient>? recipients = null,
        string mailFrom = "sender@example.test") =>
        new(
            functionName,
            RuleId: 1,
            RuleName: "rule",
            AccountId: 0,
            MailFrom: mailFrom,
            Recipients: recipients ?? [],
            messageData);

    private static IReadOnlyList<SmtpResolvedRecipient> CreateRecipients() =>
        [
            new(
                "local@example.test",
                "local@example.test",
                LocalAccountId: 42,
                IsLocal: true),
            new(
                "alias-target@example.test",
                "Alias <alias@example.test>",
                LocalAccountId: 0,
                IsLocal: false)
        ];

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
