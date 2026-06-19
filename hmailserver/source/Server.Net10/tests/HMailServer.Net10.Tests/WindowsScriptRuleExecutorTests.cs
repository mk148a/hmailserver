using System.Text;
using System.Text.RegularExpressions;
using HMailServer.Core.Abstractions;
using HMailServer.Scripting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MimeKit;

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
   If obMessage.Filename <> obMessage.FileName Then
      obMessage.RejectReason = "filename alias not loaded"
      Exit Sub
   End If
   obMessage.Filename = obMessage.FileName
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
   If obMessage.Size <= 0 Then
      obMessage.RejectReason = "size not loaded"
      Exit Sub
   End If
   If obMessage.DeliveryAttempt <> 1 Then
      obMessage.RejectReason = "delivery attempt not loaded"
      Exit Sub
   End If
   If Not obMessage.EncodeFields Then
      obMessage.RejectReason = "encode fields not loaded"
      Exit Sub
   End If
   If Not obMessage.HasBodyType("text/plain") Then
      obMessage.RejectReason = "body type not loaded"
      Exit Sub
   End If
   If obMessage.Charset <> "us-ascii" Then
      obMessage.RejectReason = "charset not loaded"
      Exit Sub
   End If
   If obMessage.Flag(128) Then
      obMessage.RejectReason = "message flag unexpectedly set"
      Exit Sub
   End If
   obMessage.Flag(128) = True
   If Not obMessage.Flag(128) Then
      obMessage.RejectReason = "message flag setter failed"
      Exit Sub
   End If

   obMessage.Subject = "Changed"
   obMessage.From = "Updated <updated@example.test>"
   obMessage.To = "next@example.test"
   obMessage.CC = "copy2@example.test"
   obMessage.Charset = "utf-8"
   obMessage.Body = "Changed body" & vbCrLf
   obMessage.HeaderValue("X-Legacy") = "yes"
   obMessage.HeaderValue("X-Flag-State") = CStr(obMessage.State)
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
                        "Content-Type: text/plain; charset=us-ascii\r\n" +
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
            StringAssert.Contains(messageText, "Content-Type: text/plain; charset=utf-8\r\n");
            StringAssert.Contains(messageText, "X-Legacy: yes\r\n");
            StringAssert.Contains(messageText, "X-Flag-State: 128\r\n");
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
  if (!obMessage.Filename || obMessage.Filename !== obMessage.FileName) {
    obMessage.RejectReason = "filename alias not loaded";
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
  if (obMessage.Size <= 0) {
    obMessage.RejectReason = "size not loaded";
    return;
  }
  if (obMessage.DeliveryAttempt !== 1) {
    obMessage.RejectReason = "delivery attempt not loaded";
    return;
  }
  if (!obMessage.EncodeFields) {
    obMessage.RejectReason = "encode fields not loaded";
    return;
  }
  if (!obMessage.HasBodyType("text/plain")) {
    obMessage.RejectReason = "body type not loaded";
    return;
  }
  if (obMessage.Charset !== "us-ascii") {
    obMessage.RejectReason = "charset not loaded";
    return;
  }
  if (obMessage.Flag(128) !== false || obMessage.GetFlag(128) !== false) {
    obMessage.RejectReason = "message flag unexpectedly set";
    return;
  }
  obMessage.SetFlag(128, true);
  if (obMessage.Flag(128) !== true || obMessage.GetFlag(128) !== true) {
    obMessage.RejectReason = "message flag setter failed";
    return;
  }
  obMessage.Flag(64, true);
  if (obMessage.Flag(64) !== true) {
    obMessage.RejectReason = "message flag method setter failed";
    return;
  }

  obMessage.Subject = "Changed JS";
  obMessage.From = "JS Sender <js@example.test>";
  obMessage.To = "js-next@example.test";
  obMessage.CC = "js-copy@example.test";
  obMessage.Charset = "utf-8";
  obMessage.Body = "Changed JS body\r\n";
  obMessage.SetHeaderValue("X-JScript", "yes");
  obMessage.SetHeaderValue("X-JScript-State", String(obMessage.State));
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
                        "Content-Type: text/plain; charset=us-ascii\r\n" +
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
            StringAssert.Contains(messageText, "Content-Type: text/plain; charset=utf-8\r\n");
            StringAssert.Contains(messageText, "X-JScript: yes\r\n");
            StringAssert.Contains(messageText, "X-JScript-State: 192\r\n");
            StringAssert.Contains(messageText, "\r\n\r\nChanged JS body\r\n");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_VbScriptMessageRefreshContentReloadsFileBackedFields()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub Rule_RefreshMessage(obMessage)
   Dim fso, outputFile
   Set fso = CreateObject("Scripting.FileSystemObject")
   Set outputFile = fso.CreateTextFile(obMessage.FileName, True, False)
   outputFile.Write "Subject: Refreshed VB" & vbCrLf & "X-Reloaded: yes" & vbCrLf & vbCrLf & "Reloaded body" & vbCrLf
   outputFile.Close

   obMessage.RefreshContent
   If obMessage.Subject <> "Refreshed VB" Then
      obMessage.RejectReason = "subject not refreshed"
      Exit Sub
   End If
   If obMessage.HeaderValue("X-Reloaded") <> "yes" Then
      obMessage.RejectReason = "header not refreshed"
      Exit Sub
   End If
   If obMessage.Body <> "Reloaded body" & vbCrLf Then
      obMessage.RejectReason = "body not refreshed"
      Exit Sub
   End If

   obMessage.HeaderValue("X-Refresh") = "vb"
   obMessage.Save
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            var result = executor.Execute(
                CreateRequest(
                    "Rule_RefreshMessage",
                    "Subject: Original\r\n\r\nOriginal body\r\n"u8.ToArray()),
                CancellationToken.None);

            Assert.IsTrue(result.Accepted, result.FailureResponse);
            Assert.IsNotNull(result.MessageData);
            var messageText = Encoding.ASCII.GetString(result.MessageData);
            StringAssert.Contains(messageText, "Subject: Refreshed VB\r\n");
            StringAssert.Contains(messageText, "X-Reloaded: yes\r\n");
            StringAssert.Contains(messageText, "X-Refresh: vb\r\n");
            StringAssert.Contains(messageText, "\r\n\r\nReloaded body\r\n");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_JScriptMessageRefreshContentReloadsFileBackedFields()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.js"),
                """
function Rule_RefreshMessage(obMessage) {
  var fso = new ActiveXObject("Scripting.FileSystemObject");
  var outputFile = fso.CreateTextFile(obMessage.FileName, true, false);
  outputFile.Write("Subject: Refreshed JS\r\nX-Reloaded: yes\r\n\r\nReloaded JS body\r\n");
  outputFile.Close();

  obMessage.RefreshContent();
  if (obMessage.Subject !== "Refreshed JS") {
    obMessage.RejectReason = "subject not refreshed";
    return;
  }
  if (obMessage.HeaderValue("X-Reloaded") !== "yes") {
    obMessage.RejectReason = "header not refreshed";
    return;
  }
  if (obMessage.Body !== "Reloaded JS body\r\n") {
    obMessage.RejectReason = "body not refreshed";
    return;
  }

  obMessage.SetHeaderValue("X-Refresh", "js");
  obMessage.Save();
}
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript, "JScript");

            var result = executor.Execute(
                CreateRequest(
                    "Rule_RefreshMessage",
                    "Subject: Original\r\n\r\nOriginal body\r\n"u8.ToArray()),
                CancellationToken.None);

            Assert.IsTrue(result.Accepted, result.FailureResponse);
            Assert.IsNotNull(result.MessageData);
            var messageText = Encoding.ASCII.GetString(result.MessageData);
            StringAssert.Contains(messageText, "Subject: Refreshed JS\r\n");
            StringAssert.Contains(messageText, "X-Reloaded: yes\r\n");
            StringAssert.Contains(messageText, "X-Refresh: js\r\n");
            StringAssert.Contains(messageText, "\r\n\r\nReloaded JS body\r\n");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_VbScriptMessageCopyCapturesCallTimeContent()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub Rule_CopyMessage(obMessage)
   obMessage.Subject = "Snapshot subject"
   obMessage.Save
   obMessage.Copy 42
   obMessage.Subject = "Final subject"
   obMessage.Save
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            var result = executor.Execute(
                CreateRequest(
                    "Rule_CopyMessage",
                    "Subject: Original subject\r\n\r\nBody\r\n"u8.ToArray()),
                CancellationToken.None);

            Assert.IsTrue(result.Accepted, result.FailureResponse);
            Assert.AreEqual(1, result.MessageCopyOperations?.Count);
            var copyOperation = result.MessageCopyOperations![0];
            Assert.AreEqual(42, copyOperation.DestinationFolderId);
            StringAssert.Contains(
                Encoding.ASCII.GetString(copyOperation.MessageData),
                "Subject: Snapshot subject\r\n");
            StringAssert.Contains(
                Encoding.ASCII.GetString(result.MessageData!),
                "Subject: Final subject\r\n");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_JScriptMessageCopyCapturesCallTimeContent()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.js"),
                """
function Rule_CopyMessage(obMessage) {
  obMessage.Subject = "JScript snapshot";
  obMessage.Save();
  obMessage.Copy(84);
  obMessage.Subject = "JScript final";
  obMessage.Save();
}
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript, "JScript");

            var result = executor.Execute(
                CreateRequest(
                    "Rule_CopyMessage",
                    "Subject: Original subject\r\n\r\nBody\r\n"u8.ToArray()),
                CancellationToken.None);

            Assert.IsTrue(result.Accepted, result.FailureResponse);
            Assert.AreEqual(1, result.MessageCopyOperations?.Count);
            var copyOperation = result.MessageCopyOperations![0];
            Assert.AreEqual(84, copyOperation.DestinationFolderId);
            StringAssert.Contains(
                Encoding.ASCII.GetString(copyOperation.MessageData),
                "Subject: JScript snapshot\r\n");
            StringAssert.Contains(
                Encoding.ASCII.GetString(result.MessageData!),
                "Subject: JScript final\r\n");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_ExposesHeaderCollectionToVbScript()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub Rule_UpdateHeaders(obMessage)
   If obMessage.Headers.Count <> 5 Then
      obMessage.RejectReason = "header count not loaded"
      Exit Sub
   End If

   Dim firstHeader, foldedHeader, removeHeader
   Set firstHeader = obMessage.Headers.Item(0)
   If firstHeader.Name <> "From" Then
      obMessage.RejectReason = "header item not loaded"
      Exit Sub
   End If

   Set foldedHeader = obMessage.Headers.ItemByName("X-Folded")
   If foldedHeader.Value <> "one two" Then
      obMessage.RejectReason = "folded header object not loaded"
      Exit Sub
   End If
   foldedHeader.Value = "changed"

   Set removeHeader = obMessage.Headers.ItemByName("X-Remove")
   removeHeader.Delete
   obMessage.Save
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            var result = executor.Execute(
                CreateRequest(
                    "Rule_UpdateHeaders",
                    Encoding.ASCII.GetBytes(
                        "From: Sender <sender@example.test>\r\n" +
                        "To: dest@example.test\r\n" +
                        "Subject: Headers\r\n" +
                        "X-Folded: one\r\n two\r\n" +
                        "X-Remove: gone\r\n" +
                        "\r\n" +
                        "Body\r\n")),
                CancellationToken.None);

            Assert.IsTrue(result.Accepted, result.FailureResponse);
            Assert.IsNotNull(result.MessageData);
            var messageText = Encoding.ASCII.GetString(result.MessageData);
            StringAssert.Contains(messageText, "X-Folded: changed\r\n");
            Assert.IsFalse(messageText.Contains("X-Remove:", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_ExposesHeaderCollectionToJScript()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.js"),
                """
function Rule_UpdateHeaders(obMessage) {
  if (obMessage.Headers.Count !== 5) {
    obMessage.RejectReason = "header count not loaded";
    return;
  }

  var firstHeader = obMessage.Headers.Item(0);
  if (firstHeader.Name !== "From") {
    obMessage.RejectReason = "header item not loaded";
    return;
  }

  var foldedHeader = obMessage.Headers.ItemByName("X-Folded");
  if (foldedHeader.Value !== "one two") {
    obMessage.RejectReason = "folded header object not loaded";
    return;
  }
  foldedHeader.Value = "changed-js";

  var removeHeader = obMessage.Headers.ItemByName("X-Remove");
  removeHeader.Delete();
  obMessage.Save();
}
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript, "JScript");

            var result = executor.Execute(
                CreateRequest(
                    "Rule_UpdateHeaders",
                    Encoding.ASCII.GetBytes(
                        "From: Sender <sender@example.test>\r\n" +
                        "To: dest@example.test\r\n" +
                        "Subject: Headers\r\n" +
                        "X-Folded: one\r\n two\r\n" +
                        "X-Remove: gone\r\n" +
                        "\r\n" +
                        "Body\r\n")),
                CancellationToken.None);

            Assert.IsTrue(result.Accepted, result.FailureResponse);
            Assert.IsNotNull(result.MessageData);
            var messageText = Encoding.ASCII.GetString(result.MessageData);
            StringAssert.Contains(messageText, "X-Folded: changed-js\r\n");
            Assert.IsFalse(messageText.Contains("X-Remove:", StringComparison.OrdinalIgnoreCase));
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
    public void Execute_ExposesAttachmentCollectionToVbScript()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub Rule_UpdateAttachments(obMessage)
   If obMessage.Attachments.Count <> 1 Then
      obMessage.RejectReason = "attachment count not loaded"
      Exit Sub
   End If

   Dim attachment, savedPath, fileSystem, savedFile, savedText
   Set attachment = obMessage.Attachments.Item(0)
   If attachment.FileName <> "hello.txt" Then
      obMessage.RejectReason = "attachment filename not loaded"
      Exit Sub
   End If
   If attachment.Size <> 5 Then
      obMessage.RejectReason = "attachment size not loaded"
      Exit Sub
   End If

   savedPath = obMessage.FileName & ".saved.txt"
   attachment.SaveAs savedPath
   Set fileSystem = CreateObject("Scripting.FileSystemObject")
   Set savedFile = fileSystem.OpenTextFile(savedPath, 1, False)
   savedText = savedFile.ReadAll
   savedFile.Close
   If savedText <> "Hello" Then
      obMessage.RejectReason = "attachment save failed"
      Exit Sub
   End If

   obMessage.Attachments.Clear
   obMessage.Attachments.Add savedPath
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            var result = executor.Execute(
                CreateRequest(
                    "Rule_UpdateAttachments",
                    CreateMultipartMessage(("hello.txt", "Hello"))),
                CancellationToken.None);

            Assert.IsTrue(result.Accepted, result.FailureResponse);
            Assert.IsNotNull(result.MessageData);
            var attachments = LoadAttachments(result.MessageData);
            Assert.AreEqual(1, attachments.Count);
            Assert.AreEqual("Hello", ReadAttachmentText(attachments[0]));
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_ExposesAttachmentDeleteToJScript()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.js"),
                """
function Rule_DeleteAttachment(obMessage) {
  if (obMessage.Attachments.Count !== 2) {
    obMessage.RejectReason = "attachment count not loaded";
    return;
  }
  if (obMessage.Attachments.Item(1).FileName !== "remove.txt") {
    obMessage.RejectReason = "attachment filename not loaded";
    return;
  }
  if (obMessage.Attachments.Item(1).Filename !== obMessage.Attachments.Item(1).FileName) {
    obMessage.RejectReason = "attachment filename alias not loaded";
    return;
  }

  obMessage.Attachments.Item(1).Delete();
}
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript, "JScript");

            var result = executor.Execute(
                CreateRequest(
                    "Rule_DeleteAttachment",
                    CreateMultipartMessage(("keep.txt", "Keep"), ("remove.txt", "Remove"))),
                CancellationToken.None);

            Assert.IsTrue(result.Accepted, result.FailureResponse);
            Assert.IsNotNull(result.MessageData);
            var attachments = LoadAttachments(result.MessageData);
            Assert.AreEqual(1, attachments.Count);
            Assert.AreEqual("keep.txt", GetAttachmentFileName(attachments[0]));
            Assert.AreEqual("Keep", ReadAttachmentText(attachments[0]));
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

    [TestMethod]
    public void Execute_RunsVbScriptOnAcceptMessageWithClientFacade()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub OnAcceptMessage(oClient, oMessage)
   If oClient.HELO <> "client.example" Then
      Result.Value = 2
      Result.Message = "helo missing"
      Exit Sub
   End If
   If oClient.Username <> "user@example.test" Then
      Result.Value = 2
      Result.Message = "username missing"
      Exit Sub
   End If
   If Not oClient.IsAuthenticated Then
      Result.Value = 2
      Result.Message = "auth missing"
      Exit Sub
   End If
   If Not oClient.Authenticated Then
      Result.Value = 2
      Result.Message = "legacy auth missing"
      Exit Sub
   End If
   If Not oClient.IsEncryptedConnection Then
      Result.Value = 2
      Result.Message = "tls missing"
      Exit Sub
   End If
   If Not oClient.EncryptedConnection Then
      Result.Value = 2
      Result.Message = "legacy tls missing"
      Exit Sub
   End If

   oMessage.HeaderValue("X-OnAccept") = oClient.Username
   oMessage.Save
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            var result = executor.Execute(
                CreateEventRequest(
                    "Subject: Event\r\n\r\nBody\r\n"u8.ToArray(),
                    new SmtpEventScriptClient(
                        "user@example.test",
                        "127.0.0.1",
                        Port: 25,
                        SessionId: 123,
                        HeloHost: "client.example",
                        IsAuthenticated: true,
                        IsEncryptedConnection: true)),
                CancellationToken.None);

            Assert.IsTrue(result.Accepted, result.FailureResponse);
            Assert.IsNotNull(result.MessageData);
            StringAssert.Contains(
                Encoding.ASCII.GetString(result.MessageData),
                "X-OnAccept: user@example.test\r\n");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_RunsJScriptOnAcceptMessageResultReject()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.js"),
                """
function OnAcceptMessage(oClient, oMessage) {
  if (oClient.Authenticated !== true) throw new Error("legacy auth");
  if (oClient.EncryptedConnection !== true) throw new Error("legacy tls");
  Result.Value = 2;
  Result.Message = "blocked";
}
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript, "JScript");

            var result = executor.Execute(
                CreateEventRequest(
                    "Subject: Reject\r\n\r\nBody\r\n"u8.ToArray(),
                    new SmtpEventScriptClient(
                        "user@example.test",
                        "127.0.0.1",
                        Port: 25,
                        SessionId: 123,
                        HeloHost: "client.example",
                        IsAuthenticated: true,
                        IsEncryptedConnection: true)),
                CancellationToken.None);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("554 blocked", result.FailureResponse);
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_RunsVbScriptClientOnlySmtpEvent()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub OnHELO(oClient)
   If oClient.HELO <> "client.example" Then
      Result.Value = 2
      Result.Message = "helo missing"
      Exit Sub
   End If
   If oClient.IPAddress <> "127.0.0.1" Then
      Result.Value = 2
      Result.Message = "ip missing"
      Exit Sub
   End If

   Result.Value = 3
   Result.Message = "try later"
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            var result = executor.Execute(
                CreateEventRequest(
                    "Subject: HELO\r\n\r\n"u8.ToArray(),
                    eventName: "OnHELO",
                    argumentShape: SmtpEventScriptArgumentShape.ClientOnly),
                CancellationToken.None);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("453 try later", result.FailureResponse);
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_ContinuesWhenOptionalSmtpEventIsMissing()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub OnClientConnect(oClient)
   Result.Value = 2
   Result.Message = "wrong event"
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            var result = executor.Execute(
                CreateEventRequest("Subject: Missing\r\n\r\nBody\r\n"u8.ToArray()),
                CancellationToken.None);

            Assert.IsTrue(result.Accepted, result.FailureResponse);
            Assert.IsFalse(result.DropMessage);
            Assert.IsNotNull(result.MessageData);
            StringAssert.Contains(
                Encoding.ASCII.GetString(result.MessageData),
                "Subject: Missing");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_RunsVbScriptDeliveryEventAndMapsResultValueToDrop()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub OnDeliveryStart(oMessage)
   oMessage.HeaderValue("X-Delivery-Event") = "start"
   oMessage.Save
   Result.Value = 1
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            var result = executor.Execute(
                CreateDeliveryEventRequest("Subject: Delivery\r\n\r\nBody\r\n"u8.ToArray()),
                CancellationToken.None);

            Assert.IsTrue(result.Succeeded, result.Error);
            Assert.IsTrue(result.DropMessage);
            Assert.IsNotNull(result.MessageData);
            StringAssert.Contains(
                Encoding.ASCII.GetString(result.MessageData),
                "X-Delivery-Event: start\r\n");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_RunsVbScriptDeliveryEventWithQueueMetadata()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub OnDeliveryStart(oMessage)
   If oMessage.ID <> 123 Then Err.Raise 1001, "test", "message id"
   If oMessage.UID <> 456 Then Err.Raise 1002, "test", "message uid"
   If oMessage.State <> 32 Then Err.Raise 1003, "test", "message state"
   If oMessage.DeliveryAttempt <> 4 Then Err.Raise 1004, "test", "delivery attempt"
   If Year(oMessage.InternalDate) <> 2026 Then Err.Raise 1005, "test", "internal date"
   oMessage.HeaderValue("X-Queue-ID") = CStr(oMessage.ID)
   oMessage.HeaderValue("X-Queue-Attempt") = CStr(oMessage.DeliveryAttempt)
   oMessage.Save
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            var result = executor.Execute(
                CreateDeliveryEventRequest(
                    "Subject: Delivery\r\n\r\nBody\r\n"u8.ToArray(),
                    messageId: 123,
                    messageUid: 456,
                    messageState: 32,
                    deliveryAttempt: 4,
                    internalDateUtc: DateTimeOffset.Parse("2026-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture)),
                CancellationToken.None);

            Assert.IsTrue(result.Succeeded, result.Error);
            Assert.IsNotNull(result.MessageData);
            var messageText = Encoding.ASCII.GetString(result.MessageData);
            StringAssert.Contains(messageText, "X-Queue-ID: 123\r\n");
            StringAssert.Contains(messageText, "X-Queue-Attempt: 4\r\n");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_RunsJScriptDeliveryEventWithQueueMetadata()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.js"),
                """
function OnDeliveryStart(oMessage) {
  if (oMessage.ID !== 123) throw new Error("message id");
  if (oMessage.UID !== 456) throw new Error("message uid");
  if (oMessage.State !== 32) throw new Error("message state");
  if (oMessage.DeliveryAttempt !== 4) throw new Error("delivery attempt");
  if (oMessage.InternalDate.getUTCFullYear() !== 2026) throw new Error("internal date");
  oMessage.SetHeaderValue("X-Queue-ID", String(oMessage.ID));
  oMessage.SetHeaderValue("X-Queue-Attempt", String(oMessage.DeliveryAttempt));
  oMessage.Save();
}
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript, language: "JScript");

            var result = executor.Execute(
                CreateDeliveryEventRequest(
                    "Subject: Delivery\r\n\r\nBody\r\n"u8.ToArray(),
                    messageId: 123,
                    messageUid: 456,
                    messageState: 32,
                    deliveryAttempt: 4,
                    internalDateUtc: DateTimeOffset.Parse("2026-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture)),
                CancellationToken.None);

            Assert.IsTrue(result.Succeeded, result.Error);
            Assert.IsNotNull(result.MessageData);
            var messageText = Encoding.ASCII.GetString(result.MessageData);
            StringAssert.Contains(messageText, "X-Queue-ID: 123\r\n");
            StringAssert.Contains(messageText, "X-Queue-Attempt: 4\r\n");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_DeliveryEventIgnoresSmtpRejectResultValues()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub OnDeliveryStart(oMessage)
   Result.Value = 2
   Result.Message = "smtp-only"
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            var result = executor.Execute(
                CreateDeliveryEventRequest("Subject: Delivery\r\n\r\nBody\r\n"u8.ToArray()),
                CancellationToken.None);

            Assert.IsTrue(result.Succeeded, result.Error);
            Assert.IsFalse(result.DropMessage);
            Assert.IsNotNull(result.MessageData);
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_RunsVbScriptDeliveryFailedEventWithRecipientAndError()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub OnDeliveryFailed(oMessage, recipient, errorMessage)
   oMessage.HeaderValue("X-Failed-Recipient") = recipient
   oMessage.HeaderValue("X-Failed-Error") = errorMessage
   oMessage.Save
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            var result = executor.Execute(
                CreateDeliveryEventRequest(
                    "Subject: Delivery\r\n\r\nBody\r\n"u8.ToArray(),
                    eventName: "OnDeliveryFailed",
                    argumentShape: DeliveryEventScriptArgumentShape.MessageRecipientAndError,
                    recipientAddress: "user@example.net",
                    errorMessage: "550 No such user."),
                CancellationToken.None);

            Assert.IsTrue(result.Succeeded, result.Error);
            Assert.IsNotNull(result.MessageData);
            var messageText = Encoding.ASCII.GetString(result.MessageData);
            StringAssert.Contains(messageText, "X-Failed-Recipient: user@example.net\r\n");
            StringAssert.Contains(messageText, "X-Failed-Error: 550 No such user.\r\n");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_RunsVbScriptExternalAccountDownloadWithFetchAccountAndMessage()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub OnExternalAccountDownload(oFetchAccount, oMessage, uid)
   If oFetchAccount.ID <> 77 Then Err.Raise 1001, "test", "fetch account id"
   If oFetchAccount.AccountID <> 42 Then Err.Raise 1002, "test", "account id"
   If oFetchAccount.Name <> "External POP3" Then Err.Raise 1003, "test", "name"
   If oFetchAccount.ServerAddress <> "pop3.example.test" Then Err.Raise 1004, "test", "server"
   If oFetchAccount.Port <> 995 Then Err.Raise 1005, "test", "port"
   If oFetchAccount.Username <> "external-user" Then Err.Raise 1006, "test", "username"
   If Not oFetchAccount.Enabled Then Err.Raise 1007, "test", "enabled"
   If Not oFetchAccount.UseSSL Then Err.Raise 1008, "test", "ssl"
   If oFetchAccount.ConnectionSecurity <> 1 Then Err.Raise 1009, "test", "connection security"
   If oFetchAccount.DaysToKeepMessages <> 14 Then Err.Raise 1010, "test", "days"
   If oFetchAccount.NextDownloadTime <> "2026-01-02 03:04:05" Then Err.Raise 1011, "test", "next download"
   If Not oFetchAccount.IsLocked Then Err.Raise 1012, "test", "locked"
   If uid <> "remote-uid-1" Then Err.Raise 1013, "test", "uid"
   If oMessage Is Nothing Then Err.Raise 1014, "test", "message"

   oMessage.HeaderValue("X-External-UID") = uid
   oMessage.Save
   Result.Value = 2
   Result.Parameter = 5
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            var result = executor.Execute(
                CreateExternalAccountDownloadRequest(
                    "Subject: External\r\n\r\nBody\r\n"u8.ToArray(),
                    remoteUid: "remote-uid-1"),
                CancellationToken.None);

            Assert.IsTrue(result.Succeeded, result.Error);
            Assert.AreEqual(ExternalAccountDownloadDeleteAction.DeleteAfterDays, result.DeleteAction);
            Assert.AreEqual(5, result.DeleteAfterDays);
            Assert.IsNotNull(result.MessageData);
            StringAssert.Contains(
                Encoding.ASCII.GetString(result.MessageData),
                "X-External-UID: remote-uid-1\r\n");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_RunsJScriptExternalAccountDownloadWithNullMessage()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.js"),
                """
function OnExternalAccountDownload(fetchAccount, message, uid) {
  if (message !== null) throw new Error("message");
  if (fetchAccount.ID !== 77) throw new Error("fetch account id");
  if (fetchAccount.AccountID !== 42) throw new Error("account id");
  if (fetchAccount.Name !== "External POP3") throw new Error("name");
  if (fetchAccount.MIMERecipientHeaders !== "To,CC") throw new Error("headers");
  if (fetchAccount.ProcessMIMERecipients !== true) throw new Error("process recipients");
  if (fetchAccount.ProcessMIMEDate !== true) throw new Error("process date");
  if (fetchAccount.UseAntiSpam !== true) throw new Error("spam");
  if (fetchAccount.UseAntiVirus !== true) throw new Error("virus");
  if (fetchAccount.EnableRouteRecipients !== true) throw new Error("routes");
  if (fetchAccount.NextDownloadTime !== "2026-01-02 03:04:05") throw new Error("next");
  if (fetchAccount.IsLocked !== true) throw new Error("locked");
  if (uid !== "remote-uid-2") throw new Error("uid");
  Result.Value = 3;
}
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript, "JScript");

            var result = executor.Execute(
                CreateExternalAccountDownloadRequest(
                    messageData: null,
                    remoteUid: "remote-uid-2"),
                CancellationToken.None);

            Assert.IsTrue(result.Succeeded, result.Error);
            Assert.AreEqual(ExternalAccountDownloadDeleteAction.NeverDelete, result.DeleteAction);
            Assert.IsNull(result.MessageData);
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_RunsVbScriptOnErrorWithLegacyArguments()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        var outputPath = Path.Combine(eventDirectory, "on-error-vb.txt");
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                $$"""
Sub OnError(iSeverity, iError, sSource, sDescription)
   Dim fileSystem, outputFile
   Set fileSystem = CreateObject("Scripting.FileSystemObject")
   Set outputFile = fileSystem.CreateTextFile("{{outputPath.Replace("\"", "\"\"")}}", True, False)
   outputFile.Write CStr(iSeverity) & "|" & CStr(iError) & "|" & sSource & "|" & sDescription
   outputFile.Close
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            executor.Execute(
                new ErrorEventScriptExecutionRequest(
                    Severity: 3,
                    ErrorCode: 5014,
                    Source: "BackupManager \"quoted\"",
                    Description: "first line\r\nsecond line"),
                CancellationToken.None);

            Assert.AreEqual(
                "3|5014|BackupManager \"quoted\"|first line\r\nsecond line",
                File.ReadAllText(outputPath));
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_RunsJScriptOnErrorWithLegacyArguments()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        var outputPath = Path.Combine(eventDirectory, "on-error-js.txt");
        try
        {
            var escapedOutputPath = outputPath
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.js"),
                $$"""
function OnError(severity, errorCode, source, description) {
  var fileSystem = new ActiveXObject("Scripting.FileSystemObject");
  var outputFile = fileSystem.CreateTextFile("{{escapedOutputPath}}", true, false);
  outputFile.Write(String(severity) + "|" + String(errorCode) + "|" + source + "|" + description);
  outputFile.Close();
}
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript, "JScript");

            executor.Execute(
                new ErrorEventScriptExecutionRequest(
                    Severity: 2,
                    ErrorCode: 5209,
                    Source: "LocalDelivery",
                    Description: "quoted \"description\"\r\nnext"),
                CancellationToken.None);

            Assert.AreEqual(
                "2|5209|LocalDelivery|quoted \"description\"\r\nnext",
                File.ReadAllText(outputPath));
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_RunsVbScriptRuleFunctionEventLogWrite()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        var eventLogPath = Path.Combine(eventDirectory, "hmailserver_events.log");
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub Rule_Log(obMessage)
   EventLog.Write "Rule: " & obMessage.Subject
   EventLog.Write "First" & vbCrLf & "Second"
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript, eventLogPath: eventLogPath);

            var result = executor.Execute(
                CreateRequest(
                    "Rule_Log",
                    "Subject: Logged\r\n\r\nBody\r\n"u8.ToArray()),
                CancellationToken.None);

            Assert.IsTrue(result.Accepted, result.FailureResponse);
            AssertLegacyEventLogLines(
                eventLogPath,
                "Rule: Logged",
                "First[nl]Second");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_RunsJScriptOnErrorEventLogWrite()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        var eventLogPath = Path.Combine(eventDirectory, "hmailserver_events.log");
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.js"),
                """
function OnError(severity, errorCode, source, description) {
  EventLog.Write("Error " + errorCode + ": " + description.replace(/\r\n/g, "|"));
}
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript, "JScript", eventLogPath);

            executor.Execute(
                new ErrorEventScriptExecutionRequest(
                    Severity: 2,
                    ErrorCode: 5209,
                    Source: "LocalDelivery",
                    Description: "first\r\nsecond"),
                CancellationToken.None);

            AssertLegacyEventLogLines(eventLogPath, "Error 5209: first|second");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_RunsVbScriptClientValidatePasswordAccept()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub OnClientValidatePassword(oAccount, password)
   If oAccount.Address = "user@example.test" And password = "script-secret" _
      And oAccount.ForwardEnabled And oAccount.ForwardAddress = "forward@example.net" _
      And oAccount.SignatureEnabled And oAccount.SignatureHTML = "<p>HTML</p>" _
      And oAccount.VacationMessageIsOn And oAccount.LastLogonTime = "2026-01-02 03:04:05" Then
      Result.Value = 0
   Else
      Result.Value = 1
   End If
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript);

            var result = executor.Execute(
                CreatePasswordValidationRequest("script-secret"),
                CancellationToken.None);

            Assert.AreEqual(ClientPasswordValidationScriptDecision.Accept, result.Decision);
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_RunsVbScriptClientValidatePasswordEventLogWrite()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        var eventLogPath = Path.Combine(eventDirectory, "hmailserver_events.log");
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.vbs"),
                """
Sub OnClientValidatePassword(oAccount, password)
   EventLog.Write "Account: " & oAccount.Address
   Result.Value = 0
End Sub
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript, eventLogPath: eventLogPath);

            var result = executor.Execute(
                CreatePasswordValidationRequest("script-secret"),
                CancellationToken.None);

            Assert.AreEqual(ClientPasswordValidationScriptDecision.Accept, result.Decision);
            AssertLegacyEventLogLines(eventLogPath, "Account: user@example.test");
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    [TestMethod]
    public void Execute_RunsJScriptClientValidatePasswordReject()
    {
        var cscript = GetCscriptPathOrInconclusive();
        var eventDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(eventDirectory, "EventHandlers.js"),
                """
function OnClientValidatePassword(oAccount, password) {
  if (oAccount.ID === 77 && password === "bad") {
    Result.Value = 1;
  }
}
""",
                Encoding.ASCII);
            var executor = CreateExecutor(eventDirectory, cscript, "JScript");

            var result = executor.Execute(
                CreatePasswordValidationRequest("bad"),
                CancellationToken.None);

            Assert.AreEqual(ClientPasswordValidationScriptDecision.Reject, result.Decision);
        }
        finally
        {
            TryDeleteDirectory(eventDirectory);
        }
    }

    private static WindowsScriptRuleExecutor CreateExecutor(
        string eventDirectory,
        string cscriptPath,
        string language = "VBScript",
        string eventLogPath = "") =>
        new(
            new WindowsScriptRuleExecutorOptions
            {
                Enabled = true,
                Language = language,
                EventDirectory = eventDirectory,
                EventLogPath = eventLogPath,
                Timeout = TimeSpan.FromSeconds(5),
                CScriptPath = cscriptPath
            });

    private static void AssertLegacyEventLogLines(string eventLogPath, params string[] expectedMessages)
    {
        Assert.IsTrue(File.Exists(eventLogPath), "Expected event log file to be created.");
        var lines = File.ReadAllLines(eventLogPath, Encoding.Unicode);
        Assert.AreEqual(expectedMessages.Length, lines.Length, string.Join(Environment.NewLine, lines));

        for (var index = 0; index < expectedMessages.Length; index++)
        {
            var parts = lines[index].Split('\t');
            Assert.AreEqual(3, parts.Length, lines[index]);
            Assert.IsTrue(int.TryParse(parts[0], out _), lines[index]);
            StringAssert.Matches(
                parts[1],
                new Regex("^\"\\d{4}-\\d{2}-\\d{2} \\d{2}:\\d{2}:\\d{2}\\.\\d{3}\"$"));
            Assert.AreEqual("\"" + expectedMessages[index] + "\"", parts[2]);
        }
    }

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

    private static SmtpEventScriptExecutionRequest CreateEventRequest(
        byte[] messageData,
        SmtpEventScriptClient? client = null,
        IReadOnlyList<SmtpResolvedRecipient>? recipients = null,
        string mailFrom = "sender@example.test",
        string eventName = "OnAcceptMessage",
        SmtpEventScriptArgumentShape argumentShape = SmtpEventScriptArgumentShape.ClientAndMessage) =>
        new(
            eventName,
            client ?? new SmtpEventScriptClient(
                Username: string.Empty,
                IPAddress: "127.0.0.1",
                Port: 25,
                SessionId: 0,
                HeloHost: "client.example",
                IsAuthenticated: false,
                IsEncryptedConnection: false),
            mailFrom,
            recipients ?? CreateRecipients(),
            messageData,
            argumentShape);

    private static DeliveryEventScriptExecutionRequest CreateDeliveryEventRequest(
        byte[] messageData,
        IReadOnlyList<SmtpResolvedRecipient>? recipients = null,
        string mailFrom = "sender@example.test",
        string eventName = "OnDeliveryStart",
        DeliveryEventScriptArgumentShape argumentShape = DeliveryEventScriptArgumentShape.MessageOnly,
        string recipientAddress = "",
        string errorMessage = "",
        long messageId = 0,
        long messageUid = 0,
        int messageState = 0,
        int deliveryAttempt = 1,
        DateTimeOffset? internalDateUtc = null) =>
        new(
            eventName,
            mailFrom,
            recipients ?? CreateRecipients(),
            messageData,
            argumentShape,
            recipientAddress,
            errorMessage,
            messageId,
            messageUid,
            messageState,
            deliveryAttempt,
            internalDateUtc);

    private static ExternalAccountDownloadScriptExecutionRequest CreateExternalAccountDownloadRequest(
        byte[]? messageData,
        string remoteUid,
        ExternalFetchAccountLease? account = null) =>
        new(
            account ?? CreateExternalFetchAccount(),
            remoteUid,
            messageData,
            MessageId: 123,
            MessageUid: 456,
            MessageState: 32,
            DeliveryAttempt: 4,
            InternalDateUtc: DateTimeOffset.Parse("2026-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture));

    private static ExternalFetchAccountLease CreateExternalFetchAccount() =>
        new(
            FetchAccountId: 77,
            AccountId: 42,
            Name: "External POP3",
            ServerAddress: "pop3.example.test",
            ServerPort: 995,
            ServerType: ExternalFetchServerType.Pop3,
            Username: "external-user",
            Password: "external-password",
            MinutesBetweenFetch: 10,
            DaysToKeep: 14,
            ProcessMimeRecipients: true,
            ProcessMimeDate: true,
            ConnectionSecurity: ExternalFetchConnectionSecurity.Ssl,
            UseAntiSpam: true,
            UseAntiVirus: true,
            EnableRouteRecipients: true,
            MimeRecipientHeaders: "To,CC",
            NextDownloadTime: "2026-01-02 03:04:05",
            IsLocked: true);

    private static ClientPasswordValidationScriptRequest CreatePasswordValidationRequest(
        string password,
        ScriptAccount? account = null) =>
        new(
            account ?? new ScriptAccount(
                AccountId: 77,
                Address: "user@example.test",
                Active: true,
                IsActiveDirectoryAccount: false,
                DomainId: 12,
                ActiveDirectoryDomain: "EXAMPLE",
                ActiveDirectoryUsername: "user",
                MaxSizeMegabytes: 1024,
                PersonFirstName: "Test",
                PersonLastName: "User",
                AdminLevel: 0,
                VacationMessageIsOn: true,
                VacationMessage: "Away",
                VacationSubject: "Out",
                VacationMessageExpires: true,
                VacationMessageExpiresDate: "2026-12-31",
                VacationMessageAbortSpamFlagged: true,
                ForwardEnabled: true,
                ForwardAddress: "forward@example.net",
                ForwardKeepOriginal: true,
                ForwardAbortSpamFlagged: true,
                SignatureEnabled: true,
                SignaturePlainText: "Plain",
                SignatureHtml: "<p>HTML</p>",
                LastLogonTime: "2026-01-02 03:04:05"),
            password);

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

    private static byte[] CreateMultipartMessage(
        params (string FileName, string Text)[] attachments)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.test"));
        message.To.Add(MailboxAddress.Parse("dest@example.test"));
        message.Subject = "Attachments";

        var builder = new BodyBuilder
        {
            TextBody = "Body"
        };
        foreach (var attachment in attachments)
        {
            builder.Attachments.Add(
                attachment.FileName,
                Encoding.ASCII.GetBytes(attachment.Text));
        }

        message.Body = builder.ToMessageBody();
        using var output = new MemoryStream();
        message.WriteTo(output);
        return output.ToArray();
    }

    private static List<MimeEntity> LoadAttachments(byte[] messageData)
    {
        using var input = new MemoryStream(messageData);
        var message = MimeMessage.Load(input);
        return [.. message.Attachments];
    }

    private static string GetAttachmentFileName(MimeEntity attachment) =>
        attachment.ContentDisposition?.FileName ??
        attachment.ContentType.Name ??
        string.Empty;

    private static string ReadAttachmentText(MimeEntity attachment)
    {
        using var output = new MemoryStream();
        if (attachment is MimePart part && part.Content is not null)
        {
            part.Content.DecodeTo(output);
        }
        else
        {
            attachment.WriteTo(output);
        }

        return Encoding.ASCII.GetString(output.ToArray());
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
