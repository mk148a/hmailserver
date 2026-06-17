using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HMailServer.Core.Abstractions;
using MimeKit;

namespace HMailServer.Scripting;

public sealed partial class WindowsScriptRuleExecutor : ISmtpRuleScriptExecutor
{
    private readonly WindowsScriptRuleExecutorOptions _options;

    public WindowsScriptRuleExecutor(WindowsScriptRuleExecutorOptions options)
    {
        _options = options;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.Timeout.Ticks, 0);
    }

    public SmtpRuleScriptExecutionResult Execute(
        SmtpRuleScriptExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_options.Enabled)
        {
            return SmtpRuleScriptExecutionResult.Continue();
        }

        if (!OperatingSystem.IsWindows())
        {
            return SmtpRuleScriptExecutionResult.Failure("SMTP rule scripting requires Windows.");
        }

        if (!ScriptFunctionNameRegex().IsMatch(request.FunctionName))
        {
            return SmtpRuleScriptExecutionResult.Failure("Invalid SMTP rule script function name.");
        }

        var language = NormalizeLanguage(_options.Language);
        if (language is null)
        {
            return SmtpRuleScriptExecutionResult.Failure("Unsupported SMTP rule script language.");
        }

        var scriptPath = GetScriptPath(language);
        if (!File.Exists(scriptPath))
        {
            return SmtpRuleScriptExecutionResult.Continue();
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "hmailserver-script-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var messagePath = Path.Combine(tempDirectory, "message.eml");
            var statusPath = Path.Combine(tempDirectory, "status.txt");
            var attachmentDirectory = Path.Combine(tempDirectory, "attachments");
            var attachmentManifestPath = Path.Combine(tempDirectory, "attachments.tsv");
            var attachmentOperationPath = Path.Combine(tempDirectory, "attachment-operations.tsv");
            var runnerPath = Path.Combine(tempDirectory, language.Extension == "vbs" ? "runner.vbs" : "runner.js");
            File.WriteAllBytes(messagePath, request.MessageData);
            WriteAttachmentManifest(messagePath, attachmentDirectory, attachmentManifestPath);
            File.WriteAllText(
                runnerPath,
                language.Extension == "vbs"
                    ? CreateVbScriptRunner(
                        scriptPath,
                        request.FunctionName,
                        messagePath,
                        statusPath,
                        attachmentManifestPath,
                        attachmentOperationPath,
                        request.MailFrom,
                        request.Recipients)
                    : CreateJScriptRunner(
                        scriptPath,
                        request.FunctionName,
                        messagePath,
                        statusPath,
                        attachmentManifestPath,
                        attachmentOperationPath,
                        request.MailFrom,
                        request.Recipients),
                Encoding.Unicode);

            var processResult = RunScript(runnerPath, cancellationToken);
            if (!processResult.Succeeded)
            {
                return SmtpRuleScriptExecutionResult.Failure(processResult.Error);
            }

            var status = ReadStatus(statusPath);
            if (status.Found)
            {
                ApplyAttachmentOperations(messagePath, attachmentOperationPath);
            }

            var messageData = File.Exists(messagePath)
                ? File.ReadAllBytes(messagePath)
                : request.MessageData;
            if (!status.Found)
            {
                return SmtpRuleScriptExecutionResult.Failure(
                    "SMTP rule script execution did not return status.",
                    messageData);
            }

            if (!string.IsNullOrWhiteSpace(status.RejectReason))
            {
                return SmtpRuleScriptExecutionResult.Failure(status.RejectReason, messageData);
            }

            return status.DropMessage
                ? SmtpRuleScriptExecutionResult.Drop(messageData)
                : SmtpRuleScriptExecutionResult.Continue(messageData);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return SmtpRuleScriptExecutionResult.Failure(
                "SMTP rule script execution failed: " + ex.Message,
                request.MessageData);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private string GetScriptPath(ScriptLanguage language)
    {
        var eventDirectory = string.IsNullOrWhiteSpace(_options.EventDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "Events")
            : _options.EventDirectory;
        return Path.Combine(eventDirectory, "EventHandlers." + language.Extension);
    }

    private ProcessResult RunScript(
        string runnerPath,
        CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = _options.CScriptPath,
            Arguments = "//NoLogo " + QuoteArgument(runnerPath),
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        var output = new StringBuilder();
        var error = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                output.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                error.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var registration = cancellationToken.Register(static state =>
        {
            var runningProcess = (Process)state!;
            TryKill(runningProcess);
        }, process);

        if (!process.WaitForExit(_options.Timeout))
        {
            TryKill(process);
            return new ProcessResult(false, "SMTP rule script execution timed out.");
        }

        process.WaitForExit();
        if (process.ExitCode == 0)
        {
            return new ProcessResult(true, string.Empty);
        }

        var combined = string.Concat(error.ToString(), output.ToString()).Trim();
        if (string.IsNullOrWhiteSpace(combined))
        {
            combined = "SMTP rule script execution failed with exit code " +
                process.ExitCode.ToString(CultureInfo.InvariantCulture) + ".";
        }

        return new ProcessResult(false, combined);
    }

    private static ScriptStatus ReadStatus(string statusPath)
    {
        if (!File.Exists(statusPath))
        {
            return new ScriptStatus(Found: false, DropMessage: false, RejectReason: string.Empty);
        }

        var dropMessage = false;
        var rejectReason = string.Empty;
        foreach (var line in File.ReadAllLines(statusPath))
        {
            if (line.Equals("DropMessage=1", StringComparison.OrdinalIgnoreCase))
            {
                dropMessage = true;
            }
            else if (line.StartsWith("RejectReason=", StringComparison.OrdinalIgnoreCase))
            {
                rejectReason = line["RejectReason=".Length..];
            }
        }

        return new ScriptStatus(Found: true, dropMessage, rejectReason);
    }

    private static void WriteAttachmentManifest(
        string messagePath,
        string attachmentDirectory,
        string manifestPath)
    {
        Directory.CreateDirectory(attachmentDirectory);

        MimeMessage message;
        try
        {
            using var input = File.OpenRead(messagePath);
            message = MimeMessage.Load(input);
        }
        catch (FormatException)
        {
            File.WriteAllText(manifestPath, string.Empty, Encoding.UTF8);
            return;
        }

        using var manifest = new StreamWriter(manifestPath, append: false, Encoding.UTF8);
        var index = 0;
        foreach (var attachment in message.Attachments)
        {
            var fileName = GetAttachmentFileName(attachment, index);
            var attachmentPath = Path.Combine(
                attachmentDirectory,
                index.ToString(CultureInfo.InvariantCulture) + "-" + SanitizeFileName(fileName));
            using (var output = File.Create(attachmentPath))
            {
                if (attachment is MimePart part && part.Content is not null)
                {
                    part.Content.DecodeTo(output);
                }
                else
                {
                    attachment.WriteTo(output);
                }
            }

            var size = new FileInfo(attachmentPath).Length;
            manifest.Write(index.ToString(CultureInfo.InvariantCulture));
            manifest.Write('\t');
            manifest.Write(ToManifestField(fileName));
            manifest.Write('\t');
            manifest.Write(size.ToString(CultureInfo.InvariantCulture));
            manifest.Write('\t');
            manifest.WriteLine(ToManifestField(attachmentPath));
            index++;
        }
    }

    private static void ApplyAttachmentOperations(
        string messagePath,
        string operationPath)
    {
        if (!File.Exists(operationPath))
        {
            return;
        }

        var operations = File.ReadAllLines(operationPath, Encoding.UTF8);
        if (operations.Length == 0)
        {
            return;
        }

        MimeMessage message;
        using (var input = File.OpenRead(messagePath))
        {
            message = MimeMessage.Load(input);
        }

        foreach (var operationLine in operations)
        {
            if (string.IsNullOrWhiteSpace(operationLine))
            {
                continue;
            }

            var fields = operationLine.Split('\t', 2);
            var operation = fields[0];
            var value = fields.Length > 1 ? fields[1] : string.Empty;
            if (operation.Equals("Clear", StringComparison.OrdinalIgnoreCase))
            {
                RemoveAllAttachments(message.Body);
            }
            else if (operation.Equals("DeleteIndex", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                var currentIndex = 0;
                RemoveAttachmentAt(message.Body, index, ref currentIndex);
            }
            else if (operation.Equals("Add", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(value))
            {
                AddAttachment(message, value);
            }
        }

        using var output = File.Create(messagePath);
        message.WriteTo(output);
    }

    private static void AddAttachment(
        MimeMessage message,
        string path)
    {
        var attachmentBytes = File.ReadAllBytes(path);
        var attachment = new MimePart("application", "octet-stream")
        {
            Content = new MimeContent(new MemoryStream(attachmentBytes)),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64,
            FileName = Path.GetFileName(path)
        };

        if (message.Body is Multipart multipart &&
            multipart.ContentType.MediaSubtype.Equals("mixed", StringComparison.OrdinalIgnoreCase))
        {
            multipart.Add(attachment);
            return;
        }

        var mixed = new Multipart("mixed");
        if (message.Body is not null)
        {
            mixed.Add(message.Body);
        }

        mixed.Add(attachment);
        message.Body = mixed;
    }

    private static void RemoveAllAttachments(MimeEntity? entity)
    {
        if (entity is not Multipart multipart)
        {
            return;
        }

        for (var index = multipart.Count - 1; index >= 0; index--)
        {
            var child = multipart[index];
            if (IsAttachmentEntity(child))
            {
                multipart.RemoveAt(index);
            }
            else
            {
                RemoveAllAttachments(child);
            }
        }
    }

    private static bool RemoveAttachmentAt(
        MimeEntity? entity,
        int targetIndex,
        ref int currentIndex)
    {
        if (entity is not Multipart multipart)
        {
            return false;
        }

        for (var index = 0; index < multipart.Count; index++)
        {
            var child = multipart[index];
            if (IsAttachmentEntity(child))
            {
                if (currentIndex == targetIndex)
                {
                    multipart.RemoveAt(index);
                    return true;
                }

                currentIndex++;
            }
            else if (RemoveAttachmentAt(child, targetIndex, ref currentIndex))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAttachmentEntity(MimeEntity entity) =>
        entity.ContentDisposition?.Disposition.Equals(
            ContentDisposition.Attachment,
            StringComparison.OrdinalIgnoreCase) == true ||
        !string.IsNullOrWhiteSpace(entity.ContentDisposition?.FileName) ||
        !string.IsNullOrWhiteSpace(entity.ContentType.Name);

    private static string GetAttachmentFileName(
        MimeEntity entity,
        int index)
    {
        var fileName = entity.ContentDisposition?.FileName;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = entity.ContentType.Name;
        }

        return string.IsNullOrWhiteSpace(fileName)
            ? "attachment-" + index.ToString(CultureInfo.InvariantCulture) + ".dat"
            : fileName;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(fileName.Length);
        foreach (var character in fileName)
        {
            builder.Append(Array.IndexOf(invalid, character) >= 0 ? '_' : character);
        }

        return builder.Length == 0 ? "attachment.dat" : builder.ToString();
    }

    private static string ToManifestField(string value) =>
        value.Replace('\t', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ');

    private static string CreateVbScriptRunner(
        string scriptPath,
        string functionName,
        string messagePath,
        string statusPath,
        string attachmentManifestPath,
        string attachmentOperationPath,
        string mailFrom,
        IReadOnlyList<SmtpResolvedRecipient> recipients)
    {
        return $$"""
ExecuteGlobal CreateObject("Scripting.FileSystemObject").OpenTextFile("{{EscapeVbScript(scriptPath)}}", 1, False).ReadAll

Class HMailServerRuleRecipient
   Public Address
   Public OriginalAddress
   Public IsLocalUser
End Class

Class HMailServerRuleRecipients
   Private m_addresses
   Private m_originalAddresses
   Private m_isLocalUsers
   Private m_count

   Private Sub Class_Initialize()
      m_count = 0
      ReDim m_addresses(0)
      ReDim m_originalAddresses(0)
      ReDim m_isLocalUsers(0)
   End Sub

   Public Property Get Count()
      Count = m_count
   End Property

   Public Function Item(index)
      If index < 0 Or index >= m_count Then
         Set Item = Nothing
         Exit Function
      End If

      Dim recipient
      Set recipient = New HMailServerRuleRecipient
      recipient.Address = m_addresses(index)
      recipient.OriginalAddress = m_originalAddresses(index)
      recipient.IsLocalUser = m_isLocalUsers(index)
      Set Item = recipient
   End Function

   Public Sub Add(address, originalAddress, isLocalUser)
      If m_count > 0 Then
         ReDim Preserve m_addresses(m_count)
         ReDim Preserve m_originalAddresses(m_count)
         ReDim Preserve m_isLocalUsers(m_count)
      End If

      m_addresses(m_count) = CStr(address)
      m_originalAddresses(m_count) = CStr(originalAddress)
      m_isLocalUsers(m_count) = CBool(isLocalUser)
      m_count = m_count + 1
   End Sub

   Public Sub Clear()
      m_count = 0
      ReDim m_addresses(0)
      ReDim m_originalAddresses(0)
      ReDim m_isLocalUsers(0)
   End Sub

   Public Function ToHeaderValue()
      Dim index, value
      value = ""
      For index = 0 To m_count - 1
         If Len(value) > 0 Then
            value = value & ", "
         End If
         value = value & m_addresses(index)
      Next
      ToHeaderValue = value
   End Function
End Class

Class HMailServerRuleAttachment
   Public FileName
   Public Size
   Private m_sourcePath
   Private m_owner
   Private m_index

   Public Sub Initialize(owner, index, attachmentFileName, attachmentSize, sourcePath)
      Set m_owner = owner
      m_index = index
      FileName = attachmentFileName
      Size = CLng(attachmentSize)
      m_sourcePath = sourcePath
   End Sub

   Public Sub SaveAs(path)
      Dim fileSystem
      Set fileSystem = CreateObject("Scripting.FileSystemObject")
      fileSystem.CopyFile m_sourcePath, CStr(path), True
   End Sub

   Public Sub Delete()
      m_owner.DeleteAt m_index
   End Sub
End Class

Class HMailServerRuleAttachments
   Private m_fileNames
   Private m_sizes
   Private m_sourcePaths
   Private m_count
   Private m_operationPath

   Private Sub Class_Initialize()
      m_count = 0
      m_operationPath = ""
      ReDim m_fileNames(0)
      ReDim m_sizes(0)
      ReDim m_sourcePaths(0)
   End Sub

   Public Sub Load(manifestPath, operationPath)
      m_operationPath = operationPath
      ClearInMemory

      Dim fileSystem
      Set fileSystem = CreateObject("Scripting.FileSystemObject")
      If Not fileSystem.FileExists(manifestPath) Then
         Exit Sub
      End If

      Dim manifestFile, line, fields
      Set manifestFile = fileSystem.OpenTextFile(manifestPath, 1, False)
      Do Until manifestFile.AtEndOfStream
         line = manifestFile.ReadLine
         fields = Split(line, vbTab)
         If UBound(fields) >= 3 Then
            AddInMemory fields(1), CLng(fields(2)), fields(3)
         End If
      Loop
      manifestFile.Close
   End Sub

   Public Property Get Count()
      Count = m_count
   End Property

   Public Function Item(index)
      If index < 0 Or index >= m_count Then
         Set Item = Nothing
         Exit Function
      End If

      Dim attachment
      Set attachment = New HMailServerRuleAttachment
      attachment.Initialize Me, index, m_fileNames(index), m_sizes(index), m_sourcePaths(index)
      Set Item = attachment
   End Function

   Public Sub Clear()
      ClearInMemory
      AppendOperation "Clear", ""
   End Sub

   Public Sub Add(path)
      Dim fileSystem, file
      Set fileSystem = CreateObject("Scripting.FileSystemObject")
      If Not fileSystem.FileExists(path) Then
         Exit Sub
      End If

      Set file = fileSystem.GetFile(path)
      AddInMemory file.Name, CLng(file.Size), CStr(path)
      AppendOperation "Add", CStr(path)
   End Sub

   Public Sub DeleteAt(index)
      If index < 0 Or index >= m_count Then
         Exit Sub
      End If

      Dim removeIndex
      AppendOperation "DeleteIndex", CStr(index)
      For removeIndex = index To m_count - 2
         m_fileNames(removeIndex) = m_fileNames(removeIndex + 1)
         m_sizes(removeIndex) = m_sizes(removeIndex + 1)
         m_sourcePaths(removeIndex) = m_sourcePaths(removeIndex + 1)
      Next
      m_count = m_count - 1
      If m_count = 0 Then
         ClearInMemory
      Else
         ReDim Preserve m_fileNames(m_count - 1)
         ReDim Preserve m_sizes(m_count - 1)
         ReDim Preserve m_sourcePaths(m_count - 1)
      End If
   End Sub

   Private Sub AddInMemory(fileName, size, sourcePath)
      If m_count > 0 Then
         ReDim Preserve m_fileNames(m_count)
         ReDim Preserve m_sizes(m_count)
         ReDim Preserve m_sourcePaths(m_count)
      End If

      m_fileNames(m_count) = CStr(fileName)
      m_sizes(m_count) = CLng(size)
      m_sourcePaths(m_count) = CStr(sourcePath)
      m_count = m_count + 1
   End Sub

   Private Sub ClearInMemory()
      m_count = 0
      ReDim m_fileNames(0)
      ReDim m_sizes(0)
      ReDim m_sourcePaths(0)
   End Sub

   Private Sub AppendOperation(name, value)
      If Len(m_operationPath) = 0 Then
         Exit Sub
      End If

      Dim fileSystem, operationFile
      Set fileSystem = CreateObject("Scripting.FileSystemObject")
      Set operationFile = fileSystem.OpenTextFile(m_operationPath, 8, True)
      operationFile.WriteLine name & vbTab & Replace(Replace(CStr(value), vbCr, " "), vbLf, " ")
      operationFile.Close
   End Sub
End Class

Class HMailServerRuleMessage
   Public FileName
   Public DropMessage
   Public RejectReason
   Public ID
   Public UID
   Public State
   Public DeliveryAttempt
   Public InternalDate
   Private m_headers
   Private m_body
   Private m_htmlBody
   Private m_subject
   Private m_from
   Private m_fromAddress
   Private m_to
   Private m_cc
   Private m_date
   Private m_charset
   Private m_encodeFields
   Private m_recipients
   Private m_attachments

   Private Sub Class_Initialize()
      Set m_recipients = New HMailServerRuleRecipients
      Set m_attachments = New HMailServerRuleAttachments
      ID = 0
      UID = 0
      State = 0
      DeliveryAttempt = 1
      InternalDate = Now
      m_encodeFields = True
   End Sub

   Public Sub Load()
      Dim messageText
      messageText = ReadAllText(FileName)
      SplitMessage messageText, m_headers, m_body
      m_subject = HeaderValue("Subject")
      m_from = HeaderValue("From")
      m_fromAddress = m_from
      m_to = HeaderValue("To")
      m_cc = HeaderValue("Cc")
      If Len(m_cc) = 0 Then
         m_cc = HeaderValue("CC")
      End If
      m_date = HeaderValue("Date")
      m_charset = ExtractCharset(HeaderValue("Content-Type"))
      If InStr(1, HeaderValue("Content-Type"), "text/html", vbTextCompare) > 0 Then
         m_htmlBody = m_body
      Else
         m_htmlBody = ""
      End If
   End Sub

   Public Sub RefreshContent()
      Load
   End Sub

   Public Sub Save()
      Dim headers, messageBody
      headers = m_headers
      headers = SetHeaderLine(headers, "Subject", m_subject)
      headers = SetHeaderLine(headers, "From", m_from)
      headers = SetHeaderLine(headers, "To", m_to)
      headers = SetHeaderLine(headers, "Cc", m_cc)
      headers = SetHeaderLine(headers, "Date", m_date)
      If Len(m_charset) > 0 Then
         headers = SetHeaderLine(headers, "Content-Type", ApplyCharset(GetHeaderLine(headers, "Content-Type"), m_charset))
      End If
      messageBody = m_body
      If Len(m_htmlBody) > 0 And InStr(1, GetHeaderLine(headers, "Content-Type"), "text/html", vbTextCompare) > 0 Then
         messageBody = m_htmlBody
      End If
      WriteAllText FileName, headers & vbCrLf & vbCrLf & messageBody
      m_headers = headers
   End Sub

   Public Property Get Subject()
      Subject = m_subject
   End Property

   Public Property Let Subject(value)
      m_subject = CStr(value)
   End Property

   Public Property Get [From]()
      [From] = m_from
   End Property

   Public Property Let [From](value)
      m_from = CStr(value)
   End Property

   Public Property Get FromAddress()
      FromAddress = m_fromAddress
   End Property

   Public Property Let FromAddress(value)
      m_fromAddress = CStr(value)
   End Property

   Public Property Get [To]()
      [To] = m_to
   End Property

   Public Property Let [To](value)
      m_to = CStr(value)
   End Property

   Public Property Get Recipients()
      Set Recipients = m_recipients
   End Property

   Public Property Get Attachments()
      Set Attachments = m_attachments
   End Property

   Public Property Get CC()
      CC = m_cc
   End Property

   Public Property Let CC(value)
      m_cc = CStr(value)
   End Property

   Public Property Get [Date]()
      [Date] = m_date
   End Property

   Public Property Let [Date](value)
      m_date = CStr(value)
   End Property

   Public Property Get Body()
      Body = m_body
   End Property

   Public Property Let Body(value)
      m_body = CStr(value)
   End Property

   Public Property Get HTMLBody()
      HTMLBody = m_htmlBody
   End Property

   Public Property Let HTMLBody(value)
      m_htmlBody = CStr(value)
   End Property

   Public Property Get HeaderValue(fieldName)
      HeaderValue = GetHeaderLine(m_headers, CStr(fieldName))
   End Property

   Public Property Let HeaderValue(fieldName, fieldValue)
      SetHeaderValue fieldName, fieldValue
   End Property

   Public Property Get Size()
      Dim fileSystem, file
      Set fileSystem = CreateObject("Scripting.FileSystemObject")
      If fileSystem.FileExists(FileName) Then
         Set file = fileSystem.GetFile(FileName)
         Size = CLng((CDbl(file.Size) + 1023) / 1024)
      Else
         Size = 0
      End If
   End Property

   Public Property Get Charset()
      Charset = m_charset
   End Property

   Public Property Let Charset(value)
      m_charset = CStr(value)
      m_headers = SetHeaderLine(m_headers, "Content-Type", ApplyCharset(HeaderValue("Content-Type"), m_charset))
   End Property

   Public Property Get EncodeFields()
      EncodeFields = m_encodeFields
   End Property

   Public Property Let EncodeFields(value)
      m_encodeFields = CBool(value)
   End Property

   Public Function HasBodyType(bodyType)
      HasBodyType = InStr(1, m_headers & vbCrLf & m_body, CStr(bodyType), vbTextCompare) > 0
   End Function

   Public Sub AddRecipient(name, address)
      Dim displayAddress
      m_recipients.Add address, address, False
      displayAddress = FormatRecipientForHeader(name, address)
      If Len(m_to) > 0 Then
         m_to = m_to & ", " & displayAddress
      Else
         m_to = displayAddress
      End If
   End Sub

   Public Sub ClearRecipients()
      m_recipients.Clear
      m_to = ""
      m_cc = ""
      m_headers = SetHeaderLine(m_headers, "To", "")
      m_headers = SetHeaderLine(m_headers, "Cc", "")
   End Sub

   Public Sub SetHeaderValue(fieldName, fieldValue)
      Dim name
      name = LCase(CStr(fieldName))
      m_headers = SetHeaderLine(m_headers, CStr(fieldName), CStr(fieldValue))
      Select Case name
         Case "subject"
            m_subject = CStr(fieldValue)
         Case "from"
            m_from = CStr(fieldValue)
            m_fromAddress = m_from
         Case "to"
            m_to = CStr(fieldValue)
         Case "cc"
            m_cc = CStr(fieldValue)
         Case "date"
            m_date = CStr(fieldValue)
      End Select
   End Sub

   Private Function ReadAllText(path)
      Dim fileSystem, textFile
      Set fileSystem = CreateObject("Scripting.FileSystemObject")
      If Not fileSystem.FileExists(path) Then
         ReadAllText = ""
         Exit Function
      End If
      Set textFile = fileSystem.OpenTextFile(path, 1, False)
      ReadAllText = textFile.ReadAll
      textFile.Close
   End Function

   Private Sub WriteAllText(path, value)
      Dim fileSystem, textFile
      Set fileSystem = CreateObject("Scripting.FileSystemObject")
      Set textFile = fileSystem.CreateTextFile(path, True, False)
      textFile.Write CStr(value)
      textFile.Close
   End Sub

   Private Sub SplitMessage(messageText, ByRef headers, ByRef body)
      Dim normalized, markerPosition
      normalized = NormalizeLineBreaks(messageText)
      markerPosition = InStr(1, normalized, vbCrLf & vbCrLf, vbBinaryCompare)
      If markerPosition > 0 Then
         headers = Left(normalized, markerPosition - 1)
         body = Mid(normalized, markerPosition + 4)
      Else
         headers = normalized
         body = ""
      End If
   End Sub

   Private Function NormalizeLineBreaks(value)
      Dim text
      text = CStr(value)
      text = Replace(text, vbCrLf, vbLf)
      text = Replace(text, vbCr, vbLf)
      NormalizeLineBreaks = Replace(text, vbLf, vbCrLf)
   End Function

   Private Function GetHeaderLine(headers, fieldName)
      Dim lines, index, line, colon, target, collecting, currentValue
      target = LCase(CStr(fieldName))
      lines = Split(NormalizeLineBreaks(headers), vbCrLf)
      collecting = False
      currentValue = ""
      For index = 0 To UBound(lines)
         line = lines(index)
         If Len(line) > 0 Then
            If IsContinuationLine(line) Then
               If collecting Then
                  currentValue = currentValue & " " & Trim(line)
               End If
            Else
               If collecting Then
                  GetHeaderLine = currentValue
                  Exit Function
               End If
               collecting = False
               colon = InStr(1, line, ":", vbBinaryCompare)
               If colon > 1 Then
                  If LCase(Trim(Left(line, colon - 1))) = target Then
                     collecting = True
                     currentValue = Trim(Mid(line, colon + 1))
                  End If
               End If
            End If
         Else
            If collecting Then
               GetHeaderLine = currentValue
               Exit Function
            End If
            collecting = False
         End If
      Next
      If collecting Then
         GetHeaderLine = currentValue
      Else
         GetHeaderLine = ""
      End If
   End Function

   Private Function SetHeaderLine(headers, fieldName, fieldValue)
      Dim lines, index, line, colon, target, output, updated, skipContinuation
      target = LCase(CStr(fieldName))
      lines = Split(NormalizeLineBreaks(headers), vbCrLf)
      output = ""
      updated = False
      skipContinuation = False
      For index = 0 To UBound(lines)
         line = lines(index)
         If Len(line) = 0 Then
            ' skip
         ElseIf skipContinuation Then
            If Not IsContinuationLine(line) Then
               skipContinuation = False
               colon = InStr(1, line, ":", vbBinaryCompare)
               If colon > 1 Then
                  If LCase(Trim(Left(line, colon - 1))) = target Then
                     If Not updated And Len(CStr(fieldValue)) > 0 Then
                        output = AppendHeaderLine(output, CanonicalHeaderName(fieldName) & ": " & SanitizeHeaderValue(fieldValue))
                     End If
                     updated = True
                     skipContinuation = True
                  Else
                     output = AppendHeaderLine(output, line)
                  End If
               Else
                  output = AppendHeaderLine(output, line)
               End If
            End If
         Else
            skipContinuation = False
            colon = InStr(1, line, ":", vbBinaryCompare)
            If colon > 1 Then
               If LCase(Trim(Left(line, colon - 1))) = target Then
                  If Not updated And Len(CStr(fieldValue)) > 0 Then
                     output = AppendHeaderLine(output, CanonicalHeaderName(fieldName) & ": " & SanitizeHeaderValue(fieldValue))
                  End If
                  updated = True
                  skipContinuation = True
               Else
                  output = AppendHeaderLine(output, line)
               End If
            Else
               output = AppendHeaderLine(output, line)
            End If
         End If
      Next
      If Not updated And Len(CStr(fieldValue)) > 0 Then
         output = AppendHeaderLine(output, CanonicalHeaderName(fieldName) & ": " & SanitizeHeaderValue(fieldValue))
      End If
      SetHeaderLine = output
   End Function

   Private Function IsContinuationLine(line)
      IsContinuationLine = Left(line, 1) = " " Or Left(line, 1) = vbTab
   End Function

   Private Function AppendHeaderLine(existing, line)
      If Len(existing) > 0 Then
         AppendHeaderLine = existing & vbCrLf & line
      Else
         AppendHeaderLine = line
      End If
   End Function

   Private Function CanonicalHeaderName(fieldName)
      Select Case LCase(CStr(fieldName))
         Case "cc"
            CanonicalHeaderName = "Cc"
         Case Else
            CanonicalHeaderName = CStr(fieldName)
      End Select
   End Function

   Private Function SanitizeHeaderValue(value)
      SanitizeHeaderValue = Replace(Replace(CStr(value), vbCr, " "), vbLf, " ")
   End Function

   Private Function ExtractCharset(contentType)
      Dim parts, index, part
      parts = Split(CStr(contentType), ";")
      For index = 0 To UBound(parts)
         part = Trim(parts(index))
         If LCase(Left(part, 8)) = "charset=" Then
            ExtractCharset = Replace(Mid(part, 9), Chr(34), "")
            Exit Function
         End If
      Next
      ExtractCharset = ""
   End Function

   Private Function ApplyCharset(contentType, charset)
      Dim baseContentType
      baseContentType = Trim(Split(CStr(contentType), ";")(0))
      If Len(baseContentType) = 0 Then
         baseContentType = "text/plain"
      End If
      If Len(CStr(charset)) > 0 Then
         ApplyCharset = baseContentType & "; charset=" & SanitizeHeaderValue(charset)
      Else
         ApplyCharset = baseContentType
      End If
   End Function

   Private Function FormatRecipientForHeader(name, address)
      If Len(CStr(name)) > 0 Then
         FormatRecipientForHeader = Chr(34) & Replace(CStr(name), Chr(34), "'") & Chr(34) & " <" & CStr(address) & ">"
      Else
         FormatRecipientForHeader = CStr(address)
      End If
   End Function
End Class

Dim HMAILSERVER_MESSAGE
Set HMAILSERVER_MESSAGE = New HMailServerRuleMessage
HMAILSERVER_MESSAGE.FileName = "{{EscapeVbScript(messagePath)}}"
HMAILSERVER_MESSAGE.DropMessage = False
HMAILSERVER_MESSAGE.RejectReason = ""
HMAILSERVER_MESSAGE.Load
HMAILSERVER_MESSAGE.Attachments.Load "{{EscapeVbScript(attachmentManifestPath)}}", "{{EscapeVbScript(attachmentOperationPath)}}"
HMAILSERVER_MESSAGE.FromAddress = "{{EscapeVbScript(mailFrom)}}"
{{CreateVbScriptRecipientSeeds(recipients)}}

Call {{functionName}}(HMAILSERVER_MESSAGE)

Dim hMailServerRuleStatusFileSystem, hMailServerRuleStatusFile
Set hMailServerRuleStatusFileSystem = CreateObject("Scripting.FileSystemObject")
Set hMailServerRuleStatusFile = hMailServerRuleStatusFileSystem.CreateTextFile("{{EscapeVbScript(statusPath)}}", True, False)
If HMAILSERVER_MESSAGE.DropMessage Then
   hMailServerRuleStatusFile.WriteLine "DropMessage=1"
Else
   hMailServerRuleStatusFile.WriteLine "DropMessage=0"
End If
hMailServerRuleStatusFile.WriteLine "RejectReason=" & Replace(Replace(CStr(HMAILSERVER_MESSAGE.RejectReason), vbCr, " "), vbLf, " ")
hMailServerRuleStatusFile.Close
""";
    }

    private static string CreateJScriptRunner(
        string scriptPath,
        string functionName,
        string messagePath,
        string statusPath,
        string attachmentManifestPath,
        string attachmentOperationPath,
        string mailFrom,
        IReadOnlyList<SmtpResolvedRecipient> recipients)
    {
        return $$"""
var hMailServerRuleFileSystem = new ActiveXObject("Scripting.FileSystemObject");

function hMailServerRuleReadAllText(path) {
  if (!hMailServerRuleFileSystem.FileExists(path)) {
    return "";
  }
  var textFile = hMailServerRuleFileSystem.OpenTextFile(path, 1, false);
  var value = textFile.ReadAll();
  textFile.Close();
  return value;
}

function hMailServerRuleWriteAllText(path, value) {
  var textFile = hMailServerRuleFileSystem.CreateTextFile(path, true, false);
  textFile.Write(String(value || ""));
  textFile.Close();
}

function hMailServerRuleNormalizeLineBreaks(value) {
  return String(value || "").replace(/\r\n/g, "\n").replace(/\r/g, "\n").replace(/\n/g, "\r\n");
}

function hMailServerRuleSplitMessage(value) {
  var normalized = hMailServerRuleNormalizeLineBreaks(value);
  var marker = normalized.indexOf("\r\n\r\n");
  if (marker < 0) {
    return { headers: normalized, body: "" };
  }
  return {
    headers: normalized.substring(0, marker),
    body: normalized.substring(marker + 4)
  };
}

function hMailServerRuleIsContinuation(line) {
  return line.length > 0 && (line.charAt(0) === " " || line.charAt(0) === "\t");
}

function hMailServerRuleGetHeader(headers, fieldName) {
  var target = String(fieldName || "").toLowerCase();
  var lines = hMailServerRuleNormalizeLineBreaks(headers).split("\r\n");
  var collecting = false;
  var currentValue = "";
  for (var index = 0; index < lines.length; index++) {
    var line = lines[index];
    if (hMailServerRuleIsContinuation(line)) {
      if (collecting) {
        currentValue += " " + line.replace(/^\s+/, "");
      }
      continue;
    }
    if (collecting) {
      return currentValue;
    }
    collecting = false;
    var colon = line.indexOf(":");
    if (colon > 0 && line.substring(0, colon).replace(/^\s+|\s+$/g, "").toLowerCase() === target) {
      collecting = true;
      currentValue = line.substring(colon + 1).replace(/^\s+|\s+$/g, "");
    }
  }
  return collecting ? currentValue : "";
}

function hMailServerRuleAppendHeaderLine(existing, line) {
  return existing ? existing + "\r\n" + line : line;
}

function hMailServerRuleCanonicalHeaderName(fieldName) {
  return String(fieldName || "").toLowerCase() === "cc" ? "Cc" : String(fieldName || "");
}

function hMailServerRuleSanitizeHeaderValue(value) {
  return String(value || "").replace(/[\r\n]/g, " ");
}

function hMailServerRuleSetHeader(headers, fieldName, fieldValue) {
  var target = String(fieldName || "").toLowerCase();
  var value = String(fieldValue || "");
  var lines = hMailServerRuleNormalizeLineBreaks(headers).split("\r\n");
  var output = "";
  var updated = false;
  var skipContinuation = false;
  for (var index = 0; index < lines.length; index++) {
    var line = lines[index];
    if (!line) {
      continue;
    }
    if (skipContinuation && hMailServerRuleIsContinuation(line)) {
      continue;
    }
    skipContinuation = false;
    var colon = line.indexOf(":");
    if (colon > 0 && line.substring(0, colon).replace(/^\s+|\s+$/g, "").toLowerCase() === target) {
      if (!updated && value.length > 0) {
        output = hMailServerRuleAppendHeaderLine(output, hMailServerRuleCanonicalHeaderName(fieldName) + ": " + hMailServerRuleSanitizeHeaderValue(value));
      }
      updated = true;
      skipContinuation = true;
      continue;
    }
    output = hMailServerRuleAppendHeaderLine(output, line);
  }
  if (!updated && value.length > 0) {
    output = hMailServerRuleAppendHeaderLine(output, hMailServerRuleCanonicalHeaderName(fieldName) + ": " + hMailServerRuleSanitizeHeaderValue(value));
  }
  return output;
}

function hMailServerRuleCreateRecipients() {
  return {
    _items: [],
    Count: 0,
    Item: function(index) {
      if (index < 0 || index >= this._items.length) {
        return null;
      }
      return this._items[index];
    },
    Add: function(address, originalAddress, isLocalUser) {
      this._items.push({
        Address: String(address || ""),
        OriginalAddress: String(originalAddress || address || ""),
        IsLocalUser: Boolean(isLocalUser)
      });
      this.Count = this._items.length;
    },
    Clear: function() {
      this._items = [];
      this.Count = 0;
    },
    ToHeaderValue: function() {
      var values = [];
      for (var index = 0; index < this._items.length; index++) {
        values.push(this._items[index].Address);
      }
      return values.join(", ");
    }
  };
}

function hMailServerRuleFormatRecipientForHeader(name, address) {
  if (String(name || "").length > 0) {
    return "\"" + String(name).replace(/"/g, "'") + "\" <" + String(address || "") + ">";
  }
  return String(address || "");
}

function hMailServerRuleExtractCharset(contentType) {
  var parts = String(contentType || "").split(";");
  for (var index = 0; index < parts.length; index++) {
    var part = parts[index].replace(/^\s+|\s+$/g, "");
    if (part.toLowerCase().indexOf("charset=") === 0) {
      return part.substring(8).replace(/"/g, "");
    }
  }
  return "";
}

function hMailServerRuleApplyCharset(contentType, charset) {
  var baseContentType = String(contentType || "").split(";")[0].replace(/^\s+|\s+$/g, "");
  if (!baseContentType) {
    baseContentType = "text/plain";
  }
  return charset ? baseContentType + "; charset=" + hMailServerRuleSanitizeHeaderValue(charset) : baseContentType;
}

function hMailServerRuleGetMessageSize(fileName) {
  if (!hMailServerRuleFileSystem.FileExists(fileName)) {
    return 0;
  }
  return Math.ceil(Number(hMailServerRuleFileSystem.GetFile(fileName).Size || 0) / 1024);
}

function hMailServerRuleAppendAttachmentOperation(operationPath, name, value) {
  if (!operationPath) {
    return;
  }
  var operationFile = hMailServerRuleFileSystem.OpenTextFile(operationPath, 8, true);
  operationFile.WriteLine(String(name || "") + "\t" + String(value || "").replace(/[\r\n]/g, " "));
  operationFile.Close();
}

function hMailServerRuleCreateAttachment(owner, index, fileName, size, sourcePath) {
  return {
    FileName: String(fileName || ""),
    Size: Number(size || 0),
    SaveAs: function(path) {
      hMailServerRuleFileSystem.CopyFile(sourcePath, String(path || ""), true);
    },
    Delete: function() {
      owner.DeleteAt(index);
    }
  };
}

function hMailServerRuleCreateAttachments(manifestPath, operationPath) {
  var collection = {
    _items: [],
    Count: 0,
    Load: function() {
      this._items = [];
      this.Count = 0;
      if (!hMailServerRuleFileSystem.FileExists(manifestPath)) {
        return;
      }
      var manifestFile = hMailServerRuleFileSystem.OpenTextFile(manifestPath, 1, false);
      while (!manifestFile.AtEndOfStream) {
        var fields = manifestFile.ReadLine().split("\t");
        if (fields.length >= 4) {
          this._items.push({
            FileName: fields[1],
            Size: Number(fields[2] || 0),
            SourcePath: fields[3]
          });
        }
      }
      manifestFile.Close();
      this.Count = this._items.length;
    },
    Item: function(index) {
      if (index < 0 || index >= this._items.length) {
        return null;
      }
      var item = this._items[index];
      return hMailServerRuleCreateAttachment(this, index, item.FileName, item.Size, item.SourcePath);
    },
    Clear: function() {
      this._items = [];
      this.Count = 0;
      hMailServerRuleAppendAttachmentOperation(operationPath, "Clear", "");
    },
    Add: function(path) {
      var filePath = String(path || "");
      if (!hMailServerRuleFileSystem.FileExists(filePath)) {
        return;
      }
      var file = hMailServerRuleFileSystem.GetFile(filePath);
      this._items.push({
        FileName: file.Name,
        Size: Number(file.Size || 0),
        SourcePath: filePath
      });
      this.Count = this._items.length;
      hMailServerRuleAppendAttachmentOperation(operationPath, "Add", filePath);
    },
    DeleteAt: function(index) {
      if (index < 0 || index >= this._items.length) {
        return;
      }
      hMailServerRuleAppendAttachmentOperation(operationPath, "DeleteIndex", String(index));
      this._items.splice(index, 1);
      this.Count = this._items.length;
    }
  };
  collection.Load();
  return collection;
}

var HMAILSERVER_MESSAGE = {
  FileName: "{{EscapeJScript(messagePath)}}",
  DropMessage: false,
  RejectReason: "",
  ID: 0,
  UID: 0,
  State: 0,
  Size: 0,
  DeliveryAttempt: 1,
  InternalDate: new Date(),
  Subject: "",
  From: "",
  FromAddress: "",
  To: "",
  Recipients: hMailServerRuleCreateRecipients(),
  CC: "",
  Date: "",
  Charset: "",
  EncodeFields: true,
  Body: "",
  HTMLBody: "",
  Attachments: hMailServerRuleCreateAttachments(
    "{{EscapeJScript(attachmentManifestPath)}}",
    "{{EscapeJScript(attachmentOperationPath)}}"),
  _headers: "",
  Load: function() {
    var parsed = hMailServerRuleSplitMessage(hMailServerRuleReadAllText(this.FileName));
    this._headers = parsed.headers;
    this.Body = parsed.body;
    this.Size = hMailServerRuleGetMessageSize(this.FileName);
    this.Subject = this.HeaderValue("Subject");
    this.From = this.HeaderValue("From");
    this.To = this.HeaderValue("To");
    this.CC = this.HeaderValue("Cc") || this.HeaderValue("CC");
    this.Date = this.HeaderValue("Date");
    this.Charset = hMailServerRuleExtractCharset(this.HeaderValue("Content-Type"));
    this.HTMLBody = hMailServerRuleGetHeader(this._headers, "Content-Type").toLowerCase().indexOf("text/html") >= 0 ? this.Body : "";
  },
  RefreshContent: function() {
    this.Load();
  },
  HeaderValue: function(fieldName) {
    return hMailServerRuleGetHeader(this._headers, fieldName);
  },
  SetHeaderValue: function(fieldName, fieldValue) {
    this._headers = hMailServerRuleSetHeader(this._headers, fieldName, fieldValue);
    var name = String(fieldName || "").toLowerCase();
    if (name === "subject") {
      this.Subject = String(fieldValue || "");
    } else if (name === "from") {
      this.From = String(fieldValue || "");
    } else if (name === "to") {
      this.To = String(fieldValue || "");
    } else if (name === "cc") {
      this.CC = String(fieldValue || "");
    } else if (name === "date") {
      this.Date = String(fieldValue || "");
    }
  },
  Save: function() {
    var headers = this._headers;
    headers = hMailServerRuleSetHeader(headers, "Subject", this.Subject);
    headers = hMailServerRuleSetHeader(headers, "From", this.From);
    headers = hMailServerRuleSetHeader(headers, "To", this.To);
    headers = hMailServerRuleSetHeader(headers, "Cc", this.CC);
    headers = hMailServerRuleSetHeader(headers, "Date", this.Date);
    if (this.Charset) {
      headers = hMailServerRuleSetHeader(headers, "Content-Type", hMailServerRuleApplyCharset(hMailServerRuleGetHeader(headers, "Content-Type"), this.Charset));
    }
    var messageBody = this.Body;
    if (this.HTMLBody && hMailServerRuleGetHeader(headers, "Content-Type").toLowerCase().indexOf("text/html") >= 0) {
      messageBody = this.HTMLBody;
    }
    hMailServerRuleWriteAllText(this.FileName, headers + "\r\n\r\n" + messageBody);
    this._headers = headers;
  },
  AddRecipient: function(name, address) {
    this.Recipients.Add(address, address, false);
    var displayAddress = hMailServerRuleFormatRecipientForHeader(name, address);
    this.To = this.To ? this.To + ", " + displayAddress : displayAddress;
  },
  ClearRecipients: function() {
    this.Recipients.Clear();
    this.To = "";
    this.CC = "";
    this._headers = hMailServerRuleSetHeader(this._headers, "To", "");
    this._headers = hMailServerRuleSetHeader(this._headers, "Cc", "");
  },
  HasBodyType: function(bodyType) {
    return (this._headers + "\r\n" + this.Body).toLowerCase().indexOf(String(bodyType || "").toLowerCase()) >= 0;
  },
  SetCharset: function(charset) {
    this.Charset = String(charset || "");
    this._headers = hMailServerRuleSetHeader(this._headers, "Content-Type", hMailServerRuleApplyCharset(this.HeaderValue("Content-Type"), this.Charset));
  }
};
HMAILSERVER_MESSAGE.Load();
HMAILSERVER_MESSAGE.FromAddress = "{{EscapeJScript(mailFrom)}}";
{{CreateJScriptRecipientSeeds(recipients)}}

var hMailServerRuleScriptFile = hMailServerRuleFileSystem.OpenTextFile("{{EscapeJScript(scriptPath)}}", 1, false);
eval(hMailServerRuleScriptFile.ReadAll());
hMailServerRuleScriptFile.Close();
{{functionName}}(HMAILSERVER_MESSAGE);
var hMailServerRuleStatusFile = hMailServerRuleFileSystem.CreateTextFile("{{EscapeJScript(statusPath)}}", true, false);
hMailServerRuleStatusFile.WriteLine(HMAILSERVER_MESSAGE.DropMessage ? "DropMessage=1" : "DropMessage=0");
hMailServerRuleStatusFile.WriteLine("RejectReason=" + String(HMAILSERVER_MESSAGE.RejectReason || "").replace(/[\r\n]/g, " "));
hMailServerRuleStatusFile.Close();
""";
    }

    private static string CreateVbScriptRecipientSeeds(
        IReadOnlyList<SmtpResolvedRecipient> recipients)
    {
        if (recipients.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var recipient in recipients)
        {
            builder.Append("HMAILSERVER_MESSAGE.Recipients.Add \"")
                .Append(EscapeVbScript(recipient.Address))
                .Append("\", \"")
                .Append(EscapeVbScript(recipient.OriginalAddress))
                .Append("\", ")
                .Append(recipient.IsLocal ? "True" : "False")
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string CreateJScriptRecipientSeeds(
        IReadOnlyList<SmtpResolvedRecipient> recipients)
    {
        if (recipients.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var recipient in recipients)
        {
            builder.Append("HMAILSERVER_MESSAGE.Recipients.Add(\"")
                .Append(EscapeJScript(recipient.Address))
                .Append("\", \"")
                .Append(EscapeJScript(recipient.OriginalAddress))
                .Append("\", ")
                .Append(recipient.IsLocal ? "true" : "false")
                .AppendLine(");");
        }

        return builder.ToString();
    }

    private static ScriptLanguage? NormalizeLanguage(string value)
    {
        if (value.Equals("VBScript", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("vbs", StringComparison.OrdinalIgnoreCase))
        {
            return new ScriptLanguage("VBScript", "vbs");
        }

        if (value.Equals("JScript", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("js", StringComparison.OrdinalIgnoreCase))
        {
            return new ScriptLanguage("JScript", "js");
        }

        return null;
    }

    private static string EscapeVbScript(string value) =>
        value.Replace("\"", "\"\"", StringComparison.Ordinal);

    private static string EscapeJScript(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string QuoteArgument(string value) =>
        "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
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

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ScriptFunctionNameRegex();

    private sealed record ScriptLanguage(string Name, string Extension);

    private sealed record ScriptStatus(bool Found, bool DropMessage, string RejectReason);

    private sealed record ProcessResult(bool Succeeded, string Error);
}
