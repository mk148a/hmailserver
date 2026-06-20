using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HMailServer.Core.Abstractions;
using MimeKit;

namespace HMailServer.Scripting;

public sealed partial class WindowsScriptRuleExecutor :
    ISmtpRuleScriptExecutor,
    ISmtpEventScriptExecutor,
    IDeliveryEventScriptExecutor,
    IExternalAccountDownloadScriptExecutor,
    IClientPasswordValidationScriptExecutor,
    IErrorEventScriptExecutor
{
    private const int MaxMessageCopyOperations = 100;
    private readonly WindowsScriptRuleExecutorOptions _options;
    private readonly object _eventLogSync = new();

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
        return ExecuteCore(
            new ScriptExecutionSpec(
                request.FunctionName,
                request.MailFrom,
                request.Recipients,
                request.MessageData,
                Client: null,
                Invocation: ScriptInvocation.RuleFunction,
                ArgumentShape: ScriptArgumentShape.ClientAndMessage,
                DeliveryRecipientAddress: string.Empty,
                DeliveryErrorMessage: string.Empty,
                MessageMetadata: CreateDefaultMessageMetadata()),
            cancellationToken);
    }

    public SmtpRuleScriptExecutionResult Execute(
        SmtpEventScriptExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteCore(
            new ScriptExecutionSpec(
                request.EventName,
                request.MailFrom,
                request.Recipients,
                request.MessageData,
                request.Client,
                ScriptInvocation.OptionalSmtpEvent,
                ToScriptArgumentShape(request.ArgumentShape),
                DeliveryRecipientAddress: string.Empty,
                DeliveryErrorMessage: string.Empty,
                MessageMetadata: CreateDefaultMessageMetadata()),
            cancellationToken);
    }

    public DeliveryEventScriptExecutionResult Execute(
        DeliveryEventScriptExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = ExecuteCore(
            new ScriptExecutionSpec(
                request.EventName,
                request.MailFrom,
                request.Recipients,
                request.MessageData,
                Client: null,
                Invocation: ScriptInvocation.OptionalDeliveryEvent,
                ArgumentShape: ToScriptArgumentShape(request.ArgumentShape),
                request.RecipientAddress,
                request.ErrorMessage,
                ToScriptMessageMetadata(request)),
            cancellationToken);

        if (!result.Accepted)
        {
            return DeliveryEventScriptExecutionResult.Failure(
                result.FailureResponse ?? "Delivery event script execution failed.",
                result.MessageData);
        }

        return result.DropMessage
            ? DeliveryEventScriptExecutionResult.Drop(result.MessageData)
            : DeliveryEventScriptExecutionResult.Continue(result.MessageData);
    }

    public ExternalAccountDownloadScriptExecutionResult Execute(
        ExternalAccountDownloadScriptExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = ExecuteCore(
            new ScriptExecutionSpec(
                "OnExternalAccountDownload",
                MailFrom: string.Empty,
                Recipients: Array.Empty<SmtpResolvedRecipient>(),
                request.MessageData,
                Client: null,
                ScriptInvocation.OptionalExternalAccountDownload,
                ScriptArgumentShape.FetchAccountMessageAndUid,
                DeliveryRecipientAddress: string.Empty,
                DeliveryErrorMessage: string.Empty,
                MessageMetadata: ToScriptMessageMetadata(request),
                FetchAccount: request.Account,
                ExternalAccountRemoteUid: request.RemoteUid),
            cancellationToken);

        if (!result.Accepted)
        {
            return ExternalAccountDownloadScriptExecutionResult.Failure(
                result.FailureResponse ?? "External account download script execution failed.",
                result.MessageData);
        }

        return ToExternalAccountDownloadResult(
            result.ResultValue,
            result.ResultParameter,
            result.MessageData);
    }

    private static ExternalAccountDownloadScriptExecutionResult ToExternalAccountDownloadResult(
        int resultValue,
        int resultParameter,
        byte[]? messageData) =>
        resultValue switch
        {
            1 => ExternalAccountDownloadScriptExecutionResult.DeleteImmediately(messageData),
            2 => ExternalAccountDownloadScriptExecutionResult.DeleteAfter(resultParameter, messageData),
            3 => ExternalAccountDownloadScriptExecutionResult.NeverDelete(messageData),
            _ => ExternalAccountDownloadScriptExecutionResult.Continue(messageData)
        };

    public ClientPasswordValidationScriptResult Execute(
        ClientPasswordValidationScriptRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled || !OperatingSystem.IsWindows())
        {
            return ClientPasswordValidationScriptResult.Continue();
        }

        var language = NormalizeLanguage(_options.Language);
        if (language is null)
        {
            return ClientPasswordValidationScriptResult.Continue();
        }

        var scriptPath = GetScriptPath(language);
        if (!File.Exists(scriptPath))
        {
            return ClientPasswordValidationScriptResult.Continue();
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "hmailserver-script-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var statusPath = Path.Combine(tempDirectory, "status.txt");
            var eventLogOperationPath = Path.Combine(tempDirectory, "event-log.txt");
            var runnerPath = Path.Combine(tempDirectory, language.Extension == "vbs" ? "runner.vbs" : "runner.js");
            File.WriteAllText(
                runnerPath,
                language.Extension == "vbs"
                    ? CreateVbScriptClientPasswordRunner(scriptPath, statusPath, eventLogOperationPath, request.Account, request.Password)
                    : CreateJScriptClientPasswordRunner(scriptPath, statusPath, eventLogOperationPath, request.Account, request.Password),
                Encoding.Unicode);

            var processResult = RunScript(runnerPath, cancellationToken);
            ApplyEventLogOperations(eventLogOperationPath);
            return processResult.Succeeded
                ? ReadClientPasswordValidationStatus(statusPath)
                : ClientPasswordValidationScriptResult.Continue();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return ClientPasswordValidationScriptResult.Continue();
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    public void Execute(
        ErrorEventScriptExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled || !OperatingSystem.IsWindows())
        {
            return;
        }

        var language = NormalizeLanguage(_options.Language);
        if (language is null)
        {
            return;
        }

        var scriptPath = GetScriptPath(language);
        if (!File.Exists(scriptPath))
        {
            return;
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "hmailserver-script-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var eventLogOperationPath = Path.Combine(tempDirectory, "event-log.txt");
            var runnerPath = Path.Combine(tempDirectory, language.Extension == "vbs" ? "runner.vbs" : "runner.js");
            File.WriteAllText(
                runnerPath,
                language.Extension == "vbs"
                    ? CreateVbScriptErrorEventRunner(scriptPath, eventLogOperationPath, request)
                    : CreateJScriptErrorEventRunner(scriptPath, eventLogOperationPath, request),
                Encoding.Unicode);
            RunScript(runnerPath, cancellationToken);
            ApplyEventLogOperations(eventLogOperationPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private SmtpRuleScriptExecutionResult ExecuteCore(
        ScriptExecutionSpec spec,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return SmtpRuleScriptExecutionResult.Continue();
        }

        if (!OperatingSystem.IsWindows())
        {
            return SmtpRuleScriptExecutionResult.Failure("SMTP rule scripting requires Windows.");
        }

        if (!ScriptFunctionNameRegex().IsMatch(spec.FunctionName))
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
            var eventLogOperationPath = Path.Combine(tempDirectory, "event-log.txt");
            var runnerPath = Path.Combine(tempDirectory, language.Extension == "vbs" ? "runner.vbs" : "runner.js");
            var hasMessage = spec.MessageData is not null;
            if (spec.MessageData is { } messageBytes)
            {
                File.WriteAllBytes(messagePath, messageBytes);
                WriteAttachmentManifest(messagePath, attachmentDirectory, attachmentManifestPath);
            }
            File.WriteAllText(
                runnerPath,
                language.Extension == "vbs"
                    ? CreateVbScriptRunner(
                        scriptPath,
                        spec.FunctionName,
                        messagePath,
                        statusPath,
                        attachmentManifestPath,
                        attachmentOperationPath,
                        eventLogOperationPath,
                        spec.MailFrom,
                        spec.Recipients,
                        spec.Client,
                        spec.Invocation,
                        spec.ArgumentShape,
                        spec.MessageMetadata,
                        spec.FetchAccount,
                        spec.ExternalAccountRemoteUid,
                        hasMessage,
                        spec.DeliveryRecipientAddress,
                        spec.DeliveryErrorMessage)
                    : CreateJScriptRunner(
                        scriptPath,
                        spec.FunctionName,
                        messagePath,
                        statusPath,
                        attachmentManifestPath,
                        attachmentOperationPath,
                        eventLogOperationPath,
                        spec.MailFrom,
                        spec.Recipients,
                        spec.Client,
                        spec.Invocation,
                        spec.ArgumentShape,
                        spec.MessageMetadata,
                        spec.FetchAccount,
                        spec.ExternalAccountRemoteUid,
                        hasMessage,
                        spec.DeliveryRecipientAddress,
                        spec.DeliveryErrorMessage),
                Encoding.Unicode);

            var processResult = RunScript(runnerPath, cancellationToken);
            ApplyEventLogOperations(eventLogOperationPath);
            if (!processResult.Succeeded)
            {
                return SmtpRuleScriptExecutionResult.Failure(processResult.Error);
            }

            var status = ReadStatus(statusPath);
            IReadOnlyList<ScriptMessageCopyOperation> messageCopyOperations = [];
            if (status.Found && hasMessage)
            {
                ApplyAttachmentOperations(messagePath, attachmentOperationPath);
                messageCopyOperations = ReadMessageCopyOperations(attachmentOperationPath);
            }

            var messageData = hasMessage && File.Exists(messagePath)
                ? File.ReadAllBytes(messagePath)
                : spec.MessageData;
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

            var outcome = status.DropMessage
                ? SmtpRuleScriptExecutionResult.Drop(messageData)
                : SmtpRuleScriptExecutionResult.Continue(messageData);
            return outcome
                .WithResult(status.ResultValue, status.ResultParameter)
                .WithMessageCopyOperations(messageCopyOperations);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return SmtpRuleScriptExecutionResult.Failure(
                "SMTP rule script execution failed: " + ex.Message,
                spec.MessageData);
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

    private static ScriptMessageMetadata CreateDefaultMessageMetadata() =>
        new(
            Id: 0,
            Uid: 0,
            State: 0,
            Flags: 0,
            DeliveryAttempt: 1,
            InternalDateUtc: DateTimeOffset.UtcNow);

    private static ScriptMessageMetadata ToScriptMessageMetadata(DeliveryEventScriptExecutionRequest request) =>
        new(
            request.MessageId,
            request.MessageUid,
            request.MessageState,
            request.MessageFlags,
            Math.Max(1, request.DeliveryAttempt),
            request.InternalDateUtc ?? DateTimeOffset.UtcNow);

    private static ScriptMessageMetadata ToScriptMessageMetadata(ExternalAccountDownloadScriptExecutionRequest request) =>
        new(
            request.MessageId,
            request.MessageUid,
            request.MessageState,
            request.MessageFlags,
            Math.Max(1, request.DeliveryAttempt),
            request.InternalDateUtc ?? DateTimeOffset.UtcNow);

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
            return new ScriptStatus(
                Found: false,
                DropMessage: false,
                RejectReason: string.Empty,
                ResultValue: 0,
                ResultParameter: 0);
        }

        var dropMessage = false;
        var rejectReason = string.Empty;
        var resultValue = 0;
        var resultParameter = 0;
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
            else if (line.StartsWith("ResultValue=", StringComparison.OrdinalIgnoreCase))
            {
                _ = int.TryParse(
                    line["ResultValue=".Length..],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out resultValue);
            }
            else if (line.StartsWith("ResultParameter=", StringComparison.OrdinalIgnoreCase))
            {
                _ = int.TryParse(
                    line["ResultParameter=".Length..],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out resultParameter);
            }
        }

        return new ScriptStatus(
            Found: true,
            dropMessage,
            rejectReason,
            resultValue,
            resultParameter);
    }

    private static ClientPasswordValidationScriptResult ReadClientPasswordValidationStatus(string statusPath)
    {
        if (!File.Exists(statusPath))
        {
            return ClientPasswordValidationScriptResult.Continue();
        }

        foreach (var line in File.ReadAllLines(statusPath))
        {
            if (!line.StartsWith("ResultValue=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!int.TryParse(
                    line["ResultValue=".Length..],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                return ClientPasswordValidationScriptResult.Continue();
            }

            return value switch
            {
                0 => ClientPasswordValidationScriptResult.Accept(),
                1 => ClientPasswordValidationScriptResult.Reject(),
                _ => ClientPasswordValidationScriptResult.Continue()
            };
        }

        return ClientPasswordValidationScriptResult.Continue();
    }

    private void ApplyEventLogOperations(string operationPath)
    {
        if (string.IsNullOrWhiteSpace(_options.EventLogPath) || !File.Exists(operationPath))
        {
            return;
        }

        try
        {
            lock (_eventLogSync)
            {
                var eventLogPath = Path.GetFullPath(_options.EventLogPath);
                var directory = Path.GetDirectoryName(eventLogPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var stream = new FileStream(
                    eventLogPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite);
                using var writer = new StreamWriter(stream, Encoding.Unicode);
                foreach (var message in File.ReadLines(operationPath, Encoding.Unicode))
                {
                    writer.Write(Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture));
                    writer.Write('\t');
                    writer.Write('"');
                    writer.Write(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
                    writer.Write("\"\t\"");
                    writer.Write(CleanEventLogMessage(message));
                    writer.WriteLine('"');
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string CleanEventLogMessage(string message) =>
        message.Replace("\r\n", "[nl]", StringComparison.Ordinal)
            .Replace("\r", "[nl]", StringComparison.Ordinal)
            .Replace("\n", "[nl]", StringComparison.Ordinal);

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

        var operations = File.ReadAllLines(operationPath, Encoding.UTF8)
            .Where(static operationLine =>
                operationLine.StartsWith("Clear\t", StringComparison.OrdinalIgnoreCase) ||
                operationLine.StartsWith("Add\t", StringComparison.OrdinalIgnoreCase) ||
                operationLine.StartsWith("DeleteIndex\t", StringComparison.OrdinalIgnoreCase))
            .ToArray();
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

    private static IReadOnlyList<ScriptMessageCopyOperation> ReadMessageCopyOperations(
        string operationPath)
    {
        if (!File.Exists(operationPath))
        {
            return [];
        }

        var expectedPathPrefix = Path.GetFullPath(operationPath) + ".copy-";
        var operations = new List<ScriptMessageCopyOperation>();
        foreach (var operationLine in File.ReadLines(operationPath, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(operationLine))
            {
                continue;
            }

            var fields = operationLine.Split('\t', 3);
            if (!fields[0].Equals("CopyFolder", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (operations.Count >= MaxMessageCopyOperations)
            {
                throw new InvalidDataException("Message.Copy exceeded the per-script operation limit.");
            }

            if (fields.Length != 3 ||
                !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var folderId) ||
                folderId <= 0)
            {
                throw new InvalidDataException("Message.Copy produced an invalid destination folder ID.");
            }

            var snapshotPath = Path.GetFullPath(fields[2]);
            if (!snapshotPath.StartsWith(expectedPathPrefix, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(snapshotPath))
            {
                throw new InvalidDataException("Message.Copy produced an invalid content snapshot.");
            }

            operations.Add(new ScriptMessageCopyOperation(folderId, File.ReadAllBytes(snapshotPath)));
        }

        return operations;
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

    private static string CreateVbScriptClientPasswordRunner(
        string scriptPath,
        string statusPath,
        string eventLogOperationPath,
        ScriptAccount account,
        string password)
    {
        return $$"""
{{CreateVbScriptEventLogFacade(eventLogOperationPath)}}
ExecuteGlobal CreateObject("Scripting.FileSystemObject").OpenTextFile("{{EscapeVbScript(scriptPath)}}", 1, False).ReadAll

Class HMailServerScriptAccount
   Public ID
   Public Address
   Public Active
   Public IsAD
   Public DomainID
   Public ADDomain
   Public ADUsername
   Public MaxSize
   Public PersonFirstName
   Public PersonLastName
   Public AdminLevel
   Public VacationMessageIsOn
   Public VacationMessage
   Public VacationSubject
   Public VacationMessageExpires
   Public VacationMessageExpiresDate
   Public VacationMessageAbortSpamFlagged
   Public ForwardEnabled
   Public ForwardAddress
   Public ForwardKeepOriginal
   Public ForwardAbortSpamFlagged
   Public SignatureEnabled
   Public SignaturePlainText
   Public SignatureHTML
   Public LastLogonTime
End Class

Class HMailServerRuleResult
   Public Value
   Public Parameter
   Public Message
End Class

Dim HMAILSERVER_ACCOUNT
Set HMAILSERVER_ACCOUNT = New HMailServerScriptAccount
HMAILSERVER_ACCOUNT.ID = {{account.AccountId.ToString(CultureInfo.InvariantCulture)}}
HMAILSERVER_ACCOUNT.Address = "{{EscapeVbScript(account.Address)}}"
HMAILSERVER_ACCOUNT.Active = {{(account.Active ? "True" : "False")}}
HMAILSERVER_ACCOUNT.IsAD = {{(account.IsActiveDirectoryAccount ? "True" : "False")}}
HMAILSERVER_ACCOUNT.DomainID = {{account.DomainId.ToString(CultureInfo.InvariantCulture)}}
HMAILSERVER_ACCOUNT.ADDomain = "{{EscapeVbScript(account.ActiveDirectoryDomain)}}"
HMAILSERVER_ACCOUNT.ADUsername = "{{EscapeVbScript(account.ActiveDirectoryUsername)}}"
HMAILSERVER_ACCOUNT.MaxSize = {{account.MaxSizeMegabytes.ToString(CultureInfo.InvariantCulture)}}
HMAILSERVER_ACCOUNT.PersonFirstName = "{{EscapeVbScript(account.PersonFirstName)}}"
HMAILSERVER_ACCOUNT.PersonLastName = "{{EscapeVbScript(account.PersonLastName)}}"
HMAILSERVER_ACCOUNT.AdminLevel = {{account.AdminLevel.ToString(CultureInfo.InvariantCulture)}}
HMAILSERVER_ACCOUNT.VacationMessageIsOn = {{(account.VacationMessageIsOn ? "True" : "False")}}
HMAILSERVER_ACCOUNT.VacationMessage = "{{EscapeVbScript(account.VacationMessage)}}"
HMAILSERVER_ACCOUNT.VacationSubject = "{{EscapeVbScript(account.VacationSubject)}}"
HMAILSERVER_ACCOUNT.VacationMessageExpires = {{(account.VacationMessageExpires ? "True" : "False")}}
HMAILSERVER_ACCOUNT.VacationMessageExpiresDate = "{{EscapeVbScript(account.VacationMessageExpiresDate)}}"
HMAILSERVER_ACCOUNT.VacationMessageAbortSpamFlagged = {{(account.VacationMessageAbortSpamFlagged ? "True" : "False")}}
HMAILSERVER_ACCOUNT.ForwardEnabled = {{(account.ForwardEnabled ? "True" : "False")}}
HMAILSERVER_ACCOUNT.ForwardAddress = "{{EscapeVbScript(account.ForwardAddress)}}"
HMAILSERVER_ACCOUNT.ForwardKeepOriginal = {{(account.ForwardKeepOriginal ? "True" : "False")}}
HMAILSERVER_ACCOUNT.ForwardAbortSpamFlagged = {{(account.ForwardAbortSpamFlagged ? "True" : "False")}}
HMAILSERVER_ACCOUNT.SignatureEnabled = {{(account.SignatureEnabled ? "True" : "False")}}
HMAILSERVER_ACCOUNT.SignaturePlainText = "{{EscapeVbScript(account.SignaturePlainText)}}"
HMAILSERVER_ACCOUNT.SignatureHTML = "{{EscapeVbScript(account.SignatureHtml)}}"
HMAILSERVER_ACCOUNT.LastLogonTime = "{{EscapeVbScript(account.LastLogonTime)}}"

Dim Result
Set Result = New HMailServerRuleResult
Result.Value = 2
Result.Message = ""

Dim hMailServerEventHandler
On Error Resume Next
Set hMailServerEventHandler = GetRef("OnClientValidatePassword")
If Err.Number <> 0 Then
   Err.Clear
   On Error GoTo 0
Else
   On Error GoTo 0
   Call hMailServerEventHandler(HMAILSERVER_ACCOUNT, "{{EscapeVbScript(password)}}")
End If

Dim hMailServerRuleStatusFileSystem, hMailServerRuleStatusFile
Set hMailServerRuleStatusFileSystem = CreateObject("Scripting.FileSystemObject")
Set hMailServerRuleStatusFile = hMailServerRuleStatusFileSystem.CreateTextFile("{{EscapeVbScript(statusPath)}}", True, False)
hMailServerRuleStatusFile.WriteLine "ResultValue=" & CStr(Result.Value)
hMailServerRuleStatusFile.Close
""";
    }

    private static string CreateVbScriptEventLogFacade(string operationPath)
    {
        return $$"""
Class HMailServerScriptEventLog
   Private m_operationPath

   Public Sub Initialize(path)
      m_operationPath = CStr(path)
   End Sub

   Public Sub Write(value)
      Dim fileSystem, operationFile, message
      message = Replace(CStr(value), vbCrLf, "[nl]")
      message = Replace(message, vbCr, "[nl]")
      message = Replace(message, vbLf, "[nl]")
      Set fileSystem = CreateObject("Scripting.FileSystemObject")
      Set operationFile = fileSystem.OpenTextFile(m_operationPath, 8, True, -1)
      operationFile.WriteLine message
      operationFile.Close
   End Sub
End Class

Dim EventLog
Set EventLog = New HMailServerScriptEventLog
EventLog.Initialize "{{EscapeVbScript(operationPath)}}"
""";
    }

    private static string CreateJScriptEventLogFacade(
        string fileSystemVariable,
        string operationPath)
    {
        return $$"""
var EventLog = {
  Write: function(value) {
    var operationFile = {{fileSystemVariable}}.OpenTextFile("{{EscapeJScript(operationPath)}}", 8, true, -1);
    operationFile.WriteLine(String(value == null ? "" : value).replace(/\r\n|\r|\n/g, "[nl]"));
    operationFile.Close();
  }
};
""";
    }

    private static string CreateVbScriptErrorEventRunner(
        string scriptPath,
        string eventLogOperationPath,
        ErrorEventScriptExecutionRequest request)
    {
        return $$"""
{{CreateVbScriptEventLogFacade(eventLogOperationPath)}}
ExecuteGlobal CreateObject("Scripting.FileSystemObject").OpenTextFile("{{EscapeVbScript(scriptPath)}}", 1, False).ReadAll

On Error Resume Next
Dim hMailServerOnError
Set hMailServerOnError = GetRef("OnError")
If Err.Number = 0 Then
   Err.Clear
   Call hMailServerOnError({{request.Severity.ToString(CultureInfo.InvariantCulture)}}, {{request.ErrorCode.ToString(CultureInfo.InvariantCulture)}}, "{{EscapeVbScript(request.Source)}}", "{{EscapeVbScript(request.Description)}}")
End If
""";
    }

    private static string CreateJScriptErrorEventRunner(
        string scriptPath,
        string eventLogOperationPath,
        ErrorEventScriptExecutionRequest request)
    {
        return $$"""
var hMailServerErrorFileSystem = new ActiveXObject("Scripting.FileSystemObject");
{{CreateJScriptEventLogFacade("hMailServerErrorFileSystem", eventLogOperationPath)}}
var hMailServerErrorScriptFile = hMailServerErrorFileSystem.OpenTextFile("{{EscapeJScript(scriptPath)}}", 1, false);
eval(hMailServerErrorScriptFile.ReadAll());
hMailServerErrorScriptFile.Close();
if (typeof OnError === "function") {
  OnError({{request.Severity.ToString(CultureInfo.InvariantCulture)}}, {{request.ErrorCode.ToString(CultureInfo.InvariantCulture)}}, "{{EscapeJScript(request.Source)}}", "{{EscapeJScript(request.Description)}}");
}
""";
    }

    private static string CreateJScriptClientPasswordRunner(
        string scriptPath,
        string statusPath,
        string eventLogOperationPath,
        ScriptAccount account,
        string password)
    {
        return $$"""
var hMailServerRuleFileSystem = new ActiveXObject("Scripting.FileSystemObject");
{{CreateJScriptEventLogFacade("hMailServerRuleFileSystem", eventLogOperationPath)}}
var hMailServerRuleScriptFile = hMailServerRuleFileSystem.OpenTextFile("{{EscapeJScript(scriptPath)}}", 1, false);
eval(hMailServerRuleScriptFile.ReadAll());
hMailServerRuleScriptFile.Close();

var HMAILSERVER_ACCOUNT = {
  ID: {{account.AccountId.ToString(CultureInfo.InvariantCulture)}},
  Address: "{{EscapeJScript(account.Address)}}",
  Active: {{(account.Active ? "true" : "false")}},
  IsAD: {{(account.IsActiveDirectoryAccount ? "true" : "false")}},
  DomainID: {{account.DomainId.ToString(CultureInfo.InvariantCulture)}},
  ADDomain: "{{EscapeJScript(account.ActiveDirectoryDomain)}}",
  ADUsername: "{{EscapeJScript(account.ActiveDirectoryUsername)}}",
  MaxSize: {{account.MaxSizeMegabytes.ToString(CultureInfo.InvariantCulture)}},
  PersonFirstName: "{{EscapeJScript(account.PersonFirstName)}}",
  PersonLastName: "{{EscapeJScript(account.PersonLastName)}}",
  AdminLevel: {{account.AdminLevel.ToString(CultureInfo.InvariantCulture)}},
  VacationMessageIsOn: {{(account.VacationMessageIsOn ? "true" : "false")}},
  VacationMessage: "{{EscapeJScript(account.VacationMessage)}}",
  VacationSubject: "{{EscapeJScript(account.VacationSubject)}}",
  VacationMessageExpires: {{(account.VacationMessageExpires ? "true" : "false")}},
  VacationMessageExpiresDate: "{{EscapeJScript(account.VacationMessageExpiresDate)}}",
  VacationMessageAbortSpamFlagged: {{(account.VacationMessageAbortSpamFlagged ? "true" : "false")}},
  ForwardEnabled: {{(account.ForwardEnabled ? "true" : "false")}},
  ForwardAddress: "{{EscapeJScript(account.ForwardAddress)}}",
  ForwardKeepOriginal: {{(account.ForwardKeepOriginal ? "true" : "false")}},
  ForwardAbortSpamFlagged: {{(account.ForwardAbortSpamFlagged ? "true" : "false")}},
  SignatureEnabled: {{(account.SignatureEnabled ? "true" : "false")}},
  SignaturePlainText: "{{EscapeJScript(account.SignaturePlainText)}}",
  SignatureHTML: "{{EscapeJScript(account.SignatureHtml)}}",
  LastLogonTime: "{{EscapeJScript(account.LastLogonTime)}}"
};

var Result = {
  Value: 2,
  Message: ""
};

if (typeof OnClientValidatePassword === "function") {
  OnClientValidatePassword(HMAILSERVER_ACCOUNT, "{{EscapeJScript(password)}}");
}

var hMailServerRuleStatusFile = hMailServerRuleFileSystem.CreateTextFile("{{EscapeJScript(statusPath)}}", true, false);
hMailServerRuleStatusFile.WriteLine("ResultValue=" + String(Result.Value));
hMailServerRuleStatusFile.Close();
""";
    }

    private static string CreateVbScriptRunner(
        string scriptPath,
        string functionName,
        string messagePath,
        string statusPath,
        string attachmentManifestPath,
        string attachmentOperationPath,
        string eventLogOperationPath,
        string mailFrom,
        IReadOnlyList<SmtpResolvedRecipient> recipients,
        SmtpEventScriptClient? client,
        ScriptInvocation invocation,
        ScriptArgumentShape argumentShape,
        ScriptMessageMetadata messageMetadata,
        ExternalFetchAccountLease? fetchAccount,
        string externalAccountRemoteUid,
        bool hasMessage,
        string deliveryRecipientAddress,
        string deliveryErrorMessage)
    {
        var isDeliveryEvent = invocation == ScriptInvocation.OptionalDeliveryEvent ? "1" : "0";
        var hasMessageFlag = hasMessage ? "1" : "0";
        var usesSmtpRejectResult = invocation is ScriptInvocation.RuleFunction or ScriptInvocation.OptionalSmtpEvent ? "1" : "0";
        return $$"""
{{CreateVbScriptEventLogFacade(eventLogOperationPath)}}
ExecuteGlobal CreateObject("Scripting.FileSystemObject").OpenTextFile("{{EscapeVbScript(scriptPath)}}", 1, False).ReadAll

Class HMailServerRuleClient
   Public Username
   Public IPAddress
   Public Port
   Public SessionID
   Public HELO
   Public IsAuthenticated
   Public IsEncryptedConnection
   Public Authenticated
   Public EncryptedConnection
   Public CipherVersion
   Public CipherName
   Public CipherBits
End Class

Class HMailServerRuleResult
   Public Value
   Public Parameter
   Public Message
End Class

Class HMailServerRuleFetchAccount
   Public ID
   Public AccountID
   Public Name
   Public ServerAddress
   Public Port
   Public ServerType
   Public Username
   Public Password
   Public MinutesBetweenFetch
   Public DaysToKeepMessages
   Public Enabled
   Public MIMERecipientHeaders
   Public ProcessMIMERecipients
   Public ProcessMIMEDate
   Public UseSSL
   Public ConnectionSecurity
   Public UseAntiSpam
   Public UseAntiVirus
   Public EnableRouteRecipients
   Public NextDownloadTime
   Public IsLocked
End Class

Class HMailServerRuleRecipient
   Private m_address
   Private m_originalAddress
   Private m_isLocalUser

   Public Sub Initialize(address, originalAddress, isLocalUser)
      m_address = CStr(address)
      m_originalAddress = CStr(originalAddress)
      m_isLocalUser = CBool(isLocalUser)
   End Sub

   Public Property Get Address()
      Address = m_address
   End Property

   Public Property Get OriginalAddress()
      OriginalAddress = m_originalAddress
   End Property

   Public Property Get IsLocalUser()
      IsLocalUser = m_isLocalUser
   End Property
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
      recipient.Initialize m_addresses(index), m_originalAddresses(index), m_isLocalUsers(index)
      Set Item = recipient
   End Function

   Public Sub AppendInternal(address, originalAddress, isLocalUser)
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

   Public Sub ClearInternal()
      m_count = 0
      ReDim m_addresses(0)
      ReDim m_originalAddresses(0)
      ReDim m_isLocalUsers(0)
   End Sub
End Class

Class HMailServerRuleAttachment
   Private m_fileName
   Private m_size
   Private m_sourcePath
   Private m_owner
   Private m_index

   Public Sub Initialize(owner, index, attachmentFileName, attachmentSize, sourcePath)
      Set m_owner = owner
      m_index = index
      m_fileName = CStr(attachmentFileName)
      m_size = CLng(attachmentSize)
      m_sourcePath = sourcePath
   End Sub

   Public Property Get FileName()
      FileName = m_fileName
   End Property

   Public Property Get Size()
      Size = m_size
   End Property

   Public Sub SaveAs(path)
      Dim fileSystem
      Set fileSystem = CreateObject("Scripting.FileSystemObject")
      fileSystem.CopyFile m_sourcePath, CStr(path), True
   End Sub

   Public Sub Delete()
      m_owner.RemoveAtInternal m_index
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

   Public Sub InitializeInternal(manifestPath, operationPath)
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

   Public Sub RemoveAtInternal(index)
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

Class HMailServerRuleMessageHeader
   Private m_owner
   Private m_index
   Private m_name
   Private m_value
   Private m_deleted

   Public Sub Initialize(owner, index, headerName, headerValue)
      Set m_owner = owner
      m_index = index
      m_name = CStr(headerName)
      m_value = CStr(headerValue)
      m_deleted = False
   End Sub

   Public Property Get Name()
      Name = m_name
   End Property

   Public Property Let Name(newValue)
      m_name = CStr(newValue)
      If Not m_deleted Then
         m_owner.SetHeaderAt m_index, m_name, m_value
      End If
   End Property

   Public Property Get Value()
      Value = m_value
   End Property

   Public Property Let Value(newValue)
      m_value = CStr(newValue)
      If Not m_deleted Then
         m_owner.SetHeaderAt m_index, m_name, m_value
      End If
   End Property

   Public Sub Delete()
      If Not m_deleted Then
         m_owner.DeleteHeaderAt m_index
         m_deleted = True
      End If
   End Sub
End Class

Class HMailServerRuleMessageHeaders
   Private m_owner

   Public Sub Initialize(owner)
      Set m_owner = owner
   End Sub

   Public Property Get Count()
      Count = m_owner.GetHeaderCount()
   End Property

   Public Function Item(index)
      If index < 0 Or index >= Count Then
         Set Item = Nothing
         Exit Function
      End If

      Dim header
      Set header = New HMailServerRuleMessageHeader
      header.Initialize m_owner, index, m_owner.GetHeaderNameAt(index), m_owner.GetHeaderValueAt(index)
      Set Item = header
   End Function

   Public Function ItemByName(name)
      Dim index
      index = m_owner.FindHeaderIndexByName(CStr(name))
      If index < 0 Then
         Set ItemByName = Nothing
         Exit Function
      End If

      Set ItemByName = Item(index)
   End Function

End Class

Class HMailServerRuleMessage
   Public DropMessage
   Public RejectReason
   Private m_id
   Private m_uid
   Private m_state
   Private m_flags
   Private m_deliveryAttempt
   Private m_internalDate
   Private m_fileName
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
   Private m_messageHeaders
   Private m_operationPath
   Private m_copySequence

   Private Sub Class_Initialize()
      Set m_recipients = New HMailServerRuleRecipients
      Set m_attachments = New HMailServerRuleAttachments
      Set m_messageHeaders = New HMailServerRuleMessageHeaders
      m_messageHeaders.Initialize Me
      m_id = 0
      m_uid = 0
      m_state = 0
      m_flags = 0
      m_deliveryAttempt = 1
      m_internalDate = Now
      m_fileName = ""
      m_encodeFields = True
      m_operationPath = ""
      m_copySequence = 0
   End Sub

   Public Sub InitializeFileName(value)
      m_fileName = CStr(value)
   End Sub

   Public Sub InitializeOperationPath(value)
      m_operationPath = CStr(value)
   End Sub

   Public Sub InitializeMetadata(messageID, messageUID, messageState, messageFlags, messageDeliveryAttempt, messageInternalDate)
      m_id = messageID
      m_uid = CLng(messageUID)
      m_state = CLng(messageState)
      m_flags = CLng(messageFlags)
      m_deliveryAttempt = CLng(messageDeliveryAttempt)
      m_internalDate = CDate(messageInternalDate)
   End Sub

   Public Property Get FileName()
      FileName = m_fileName
   End Property

   Public Property Get ID()
      ID = m_id
   End Property

   Public Property Get UID()
      UID = m_uid
   End Property

   Public Property Get State()
      State = m_state
   End Property

   Public Property Get DeliveryAttempt()
      DeliveryAttempt = m_deliveryAttempt
   End Property

   Public Property Get InternalDate()
      InternalDate = m_internalDate
   End Property

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

   Public Sub Copy(destinationFolderID)
      Dim folderID, fileSystem, snapshotPath, operationFile
      folderID = CLng(destinationFolderID)
      If folderID <= 0 Then
         Err.Raise vbObjectError + 1000, "HMailServerRuleMessage.Copy", "Invalid destination folder ID."
      End If
      If Len(m_operationPath) = 0 Then
         Err.Raise vbObjectError + 1001, "HMailServerRuleMessage.Copy", "Message copy operations are unavailable."
      End If

      snapshotPath = m_operationPath & ".copy-" & CStr(m_copySequence) & ".eml"
      m_copySequence = m_copySequence + 1
      Set fileSystem = CreateObject("Scripting.FileSystemObject")
      fileSystem.CopyFile FileName, snapshotPath, True
      Set operationFile = fileSystem.OpenTextFile(m_operationPath, 8, True)
      operationFile.WriteLine "CopyFolder" & vbTab & CStr(folderID) & vbTab & snapshotPath
      operationFile.Close
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

   Public Property Get Recipients()
      Set Recipients = m_recipients
   End Property

   Public Property Get Attachments()
      Set Attachments = m_attachments
   End Property

   Public Property Get Headers()
      Set Headers = m_messageHeaders
   End Property

   Public Property Get CC()
      CC = m_cc
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
         Size = CLng(Fix(CDbl(file.Size) / 1024))
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

   Public Property Get Flag(flagValue)
      Flag = (m_flags And CLng(flagValue)) <> 0
   End Property

   Public Property Let Flag(flagValue, enabled)
      If CBool(enabled) Then
         m_flags = m_flags Or CLng(flagValue)
      Else
         m_flags = m_flags And (Not CLng(flagValue))
      End If
   End Property

   Public Function HasBodyType(bodyType)
      HasBodyType = HasMimeBodyType(m_headers, m_body, CStr(bodyType), 0)
   End Function

   Public Sub AddRecipient(name, address)
      Dim displayAddress
      m_recipients.AppendInternal address, address, False
      displayAddress = FormatRecipientForHeader(name, address)
      If Len(m_to) > 0 Then
         m_to = m_to & ", " & displayAddress
      Else
         m_to = displayAddress
      End If
   End Sub

   Public Sub ClearRecipients()
      m_recipients.ClearInternal
      m_to = ""
      m_cc = ""
      m_headers = SetHeaderLine(m_headers, "To", "")
      m_headers = SetHeaderLine(m_headers, "Cc", "")
      m_headers = SetHeaderLine(m_headers, "Bcc", "")
   End Sub

   Public Sub SetHeaderValue(fieldName, fieldValue)
      Dim name
      name = LCase(CStr(fieldName))
      m_headers = SetHeaderLine(m_headers, CStr(fieldName), CStr(fieldValue))
      SyncCommonHeader fieldName, fieldValue
   End Sub

   Public Function GetHeaderCount()
      Dim names, values, count
      BuildHeaderArrays names, values, count
      GetHeaderCount = count
   End Function

   Public Function GetHeaderNameAt(index)
      Dim names, values, count
      BuildHeaderArrays names, values, count
      If index >= 0 And index < count Then
         GetHeaderNameAt = names(index)
      Else
         GetHeaderNameAt = ""
      End If
   End Function

   Public Function GetHeaderValueAt(index)
      Dim names, values, count
      BuildHeaderArrays names, values, count
      If index >= 0 And index < count Then
         GetHeaderValueAt = values(index)
      Else
         GetHeaderValueAt = ""
      End If
   End Function

   Public Function FindHeaderIndexByName(fieldName)
      Dim names, values, count, index, target
      target = LCase(CStr(fieldName))
      BuildHeaderArrays names, values, count
      For index = 0 To count - 1
         If LCase(names(index)) = target Then
            FindHeaderIndexByName = index
            Exit Function
         End If
      Next
      FindHeaderIndexByName = -1
   End Function

   Public Sub SetHeaderAt(index, fieldName, fieldValue)
      Dim names, values, count, itemIndex, output
      BuildHeaderArrays names, values, count
      If index < 0 Or index >= count Then
         Exit Sub
      End If

      names(index) = CStr(fieldName)
      values(index) = CStr(fieldValue)
      output = ""
      For itemIndex = 0 To count - 1
         If Len(values(itemIndex)) > 0 Then
            output = AppendHeaderLine(output, CanonicalHeaderName(names(itemIndex)) & ": " & SanitizeHeaderValue(values(itemIndex)))
         End If
      Next
      m_headers = output
      SyncCommonHeader fieldName, fieldValue
   End Sub

   Public Sub DeleteHeaderAt(index)
      Dim names, values, count, itemIndex, output
      BuildHeaderArrays names, values, count
      If index < 0 Or index >= count Then
         Exit Sub
      End If

      output = ""
      For itemIndex = 0 To count - 1
         If itemIndex <> index Then
            output = AppendHeaderLine(output, CanonicalHeaderName(names(itemIndex)) & ": " & SanitizeHeaderValue(values(itemIndex)))
         Else
            SyncCommonHeader names(itemIndex), ""
         End If
      Next
      m_headers = output
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

   Private Sub BuildHeaderArrays(ByRef names, ByRef values, ByRef count)
      Dim lines, index, line, colon, currentName, currentValue
      count = 0
      ReDim names(0)
      ReDim values(0)
      currentName = ""
      currentValue = ""
      lines = Split(NormalizeLineBreaks(m_headers), vbCrLf)
      For index = 0 To UBound(lines)
         line = lines(index)
         If Len(line) > 0 Then
            If IsContinuationLine(line) Then
               If Len(currentName) > 0 Then
                  currentValue = currentValue & " " & Trim(line)
               End If
            Else
               If Len(currentName) > 0 Then
                  AddHeaderArrayItem names, values, count, currentName, currentValue
               End If
               colon = InStr(1, line, ":", vbBinaryCompare)
               If colon > 1 Then
                  currentName = Trim(Left(line, colon - 1))
                  currentValue = Trim(Mid(line, colon + 1))
               Else
                  currentName = ""
                  currentValue = ""
               End If
            End If
         End If
      Next
      If Len(currentName) > 0 Then
         AddHeaderArrayItem names, values, count, currentName, currentValue
      End If
   End Sub

   Private Sub AddHeaderArrayItem(ByRef names, ByRef values, ByRef count, headerName, headerValue)
      ReDim Preserve names(count)
      ReDim Preserve values(count)
      names(count) = CStr(headerName)
      values(count) = CStr(headerValue)
      count = count + 1
   End Sub

   Private Sub SyncCommonHeader(fieldName, fieldValue)
      Select Case LCase(CStr(fieldName))
         Case "subject"
            m_subject = CStr(fieldValue)
         Case "from"
            m_from = CStr(fieldValue)
         Case "to"
            m_to = CStr(fieldValue)
         Case "cc"
            m_cc = CStr(fieldValue)
         Case "date"
            m_date = CStr(fieldValue)
         Case "content-type"
            m_charset = ExtractCharset(CStr(fieldValue))
      End Select
   End Sub

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

   Private Function HasMimeBodyType(ByVal headers, ByVal body, ByVal bodyType, ByVal depth)
      Dim requestedType, contentType, mediaType, boundary, marker
      requestedType = LCase(CStr(bodyType))
      If Len(requestedType) = 0 Then
         HasMimeBodyType = False
         Exit Function
      End If

      contentType = GetHeaderLine(headers, "Content-Type")
      mediaType = CleanMimeContentType(contentType)
      If mediaType = requestedType Then
         HasMimeBodyType = True
         Exit Function
      End If
      If depth >= 2 Or Left(mediaType, 10) <> "multipart/" Then
         HasMimeBodyType = False
         Exit Function
      End If

      boundary = ExtractMimeBoundary(contentType)
      If Len(boundary) = 0 Then
         HasMimeBodyType = False
         Exit Function
      End If

      Dim parts, index, part, partHeaders, partBody
      marker = vbCrLf & "--" & boundary
      parts = Split(vbCrLf & NormalizeLineBreaks(body), marker)
      For index = 1 To UBound(parts)
         part = parts(index)
         If Left(part, 2) = "--" Then
            Exit For
         End If
         If Left(part, 2) = vbCrLf Then
            part = Mid(part, 3)
         End If
         SplitMessage part, partHeaders, partBody
         If HasMimeBodyType(partHeaders, partBody, requestedType, depth + 1) Then
            HasMimeBodyType = True
            Exit Function
         End If
      Next

      HasMimeBodyType = False
   End Function

   Private Function CleanMimeContentType(ByVal contentType)
      Dim mediaType
      mediaType = LCase(Trim(Split(CStr(contentType), ";")(0)))
      If Len(mediaType) = 0 Then
         mediaType = "text/plain"
      End If
      CleanMimeContentType = mediaType
   End Function

   Private Function ExtractMimeBoundary(ByVal contentType)
      Dim text, index, character, inQuotes, parameterStart, parameter, equalsPosition, value
      text = CStr(contentType)
      inQuotes = False
      parameterStart = 1
      For index = 1 To Len(text) + 1
         If index <= Len(text) Then
            character = Mid(text, index, 1)
            If character = Chr(34) Then
               inQuotes = Not inQuotes
            End If
         Else
            character = ";"
         End If

         If character = ";" And Not inQuotes Then
            parameter = Trim(Mid(text, parameterStart, index - parameterStart))
            equalsPosition = InStr(1, parameter, "=", vbBinaryCompare)
            If equalsPosition > 1 Then
               If LCase(Trim(Left(parameter, equalsPosition - 1))) = "boundary" Then
                  value = Trim(Mid(parameter, equalsPosition + 1))
                  If Len(value) >= 2 And Left(value, 1) = Chr(34) And Right(value, 1) = Chr(34) Then
                     value = Mid(value, 2, Len(value) - 2)
                  End If
                  ExtractMimeBoundary = value
                  Exit Function
               End If
            End If
            parameterStart = index + 1
         End If
      Next
      ExtractMimeBoundary = ""
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
If "{{hasMessageFlag}}" = "1" Then
   Set HMAILSERVER_MESSAGE = New HMailServerRuleMessage
   HMAILSERVER_MESSAGE.InitializeFileName "{{EscapeVbScript(messagePath)}}"
   HMAILSERVER_MESSAGE.InitializeOperationPath "{{EscapeVbScript(attachmentOperationPath)}}"
   HMAILSERVER_MESSAGE.DropMessage = False
   HMAILSERVER_MESSAGE.RejectReason = ""
   HMAILSERVER_MESSAGE.Load
   HMAILSERVER_MESSAGE.Attachments.InitializeInternal "{{EscapeVbScript(attachmentManifestPath)}}", "{{EscapeVbScript(attachmentOperationPath)}}"
   HMAILSERVER_MESSAGE.FromAddress = "{{EscapeVbScript(mailFrom)}}"
   Call HMAILSERVER_MESSAGE.InitializeMetadata({{messageMetadata.Id.ToString(CultureInfo.InvariantCulture)}}, {{messageMetadata.Uid.ToString(CultureInfo.InvariantCulture)}}, {{messageMetadata.State.ToString(CultureInfo.InvariantCulture)}}, {{messageMetadata.Flags.ToString(CultureInfo.InvariantCulture)}}, {{messageMetadata.DeliveryAttempt.ToString(CultureInfo.InvariantCulture)}}, {{CreateVbScriptDateExpression(messageMetadata.InternalDateUtc)}})
{{CreateVbScriptRecipientSeeds(recipients)}}
Else
   Set HMAILSERVER_MESSAGE = Nothing
End If

Dim HMAILSERVER_FETCHACCOUNT
Set HMAILSERVER_FETCHACCOUNT = New HMailServerRuleFetchAccount
{{CreateVbScriptFetchAccountSeed(fetchAccount)}}

Dim hMailServerRuleExternalAccountRemoteUid
hMailServerRuleExternalAccountRemoteUid = "{{EscapeVbScript(externalAccountRemoteUid)}}"

Dim hMailServerRuleDeliveryRecipient
hMailServerRuleDeliveryRecipient = "{{EscapeVbScript(deliveryRecipientAddress)}}"
Dim hMailServerRuleDeliveryErrorMessage
hMailServerRuleDeliveryErrorMessage = "{{EscapeVbScript(deliveryErrorMessage)}}"

Dim HMAILSERVER_CLIENT
Set HMAILSERVER_CLIENT = New HMailServerRuleClient
{{CreateVbScriptClientSeed(client)}}

Dim Result
Set Result = New HMailServerRuleResult
Result.Value = 0
Result.Parameter = 0
Result.Message = ""

{{CreateVbScriptInvocation(functionName, invocation, argumentShape)}}

Dim hMailServerRuleStatusFileSystem, hMailServerRuleStatusFile
Set hMailServerRuleStatusFileSystem = CreateObject("Scripting.FileSystemObject")
Set hMailServerRuleStatusFile = hMailServerRuleStatusFileSystem.CreateTextFile("{{EscapeVbScript(statusPath)}}", True, False)
If "{{isDeliveryEvent}}" = "1" And "{{hasMessageFlag}}" = "1" And Result.Value = 1 Then
   HMAILSERVER_MESSAGE.DropMessage = True
End If
If "{{hasMessageFlag}}" = "1" And HMAILSERVER_MESSAGE.DropMessage Then
   hMailServerRuleStatusFile.WriteLine "DropMessage=1"
Else
   hMailServerRuleStatusFile.WriteLine "DropMessage=0"
End If
Dim hMailServerRuleRejectReason
hMailServerRuleRejectReason = ""
If "{{usesSmtpRejectResult}}" = "1" Then
   If "{{hasMessageFlag}}" = "1" Then
      hMailServerRuleRejectReason = CStr(HMAILSERVER_MESSAGE.RejectReason)
   End If
   If Result.Value = 1 Then
      hMailServerRuleRejectReason = "554 Rejected"
   ElseIf Result.Value = 2 Then
      hMailServerRuleRejectReason = "554 " & CStr(Result.Message)
   ElseIf Result.Value = 3 Then
      hMailServerRuleRejectReason = "453 " & CStr(Result.Message)
   End If
End If
hMailServerRuleStatusFile.WriteLine "RejectReason=" & Replace(Replace(hMailServerRuleRejectReason, vbCr, " "), vbLf, " ")
hMailServerRuleStatusFile.WriteLine "ResultValue=" & CStr(Result.Value)
hMailServerRuleStatusFile.WriteLine "ResultParameter=" & CStr(Result.Parameter)
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
        string eventLogOperationPath,
        string mailFrom,
        IReadOnlyList<SmtpResolvedRecipient> recipients,
        SmtpEventScriptClient? client,
        ScriptInvocation invocation,
        ScriptArgumentShape argumentShape,
        ScriptMessageMetadata messageMetadata,
        ExternalFetchAccountLease? fetchAccount,
        string externalAccountRemoteUid,
        bool hasMessage,
        string deliveryRecipientAddress,
        string deliveryErrorMessage)
    {
        var isDeliveryEvent = invocation == ScriptInvocation.OptionalDeliveryEvent ? "1" : "0";
        var hasMessageFlag = hasMessage ? "1" : "0";
        var usesSmtpRejectResult = invocation is ScriptInvocation.RuleFunction or ScriptInvocation.OptionalSmtpEvent ? "1" : "0";
        return $$"""
var hMailServerRuleFileSystem = new ActiveXObject("Scripting.FileSystemObject");
{{CreateJScriptEventLogFacade("hMailServerRuleFileSystem", eventLogOperationPath)}}

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

function hMailServerRuleParseHeaderItems(headers) {
  var lines = hMailServerRuleNormalizeLineBreaks(headers).split("\r\n");
  var items = [];
  var current = null;
  for (var index = 0; index < lines.length; index++) {
    var line = lines[index];
    if (!line) {
      continue;
    }
    if (hMailServerRuleIsContinuation(line)) {
      if (current !== null) {
        current.Value += " " + line.replace(/^\s+/, "");
      }
      continue;
    }
    var colon = line.indexOf(":");
    if (colon > 0) {
      current = {
        Name: line.substring(0, colon).replace(/^\s+|\s+$/g, ""),
        Value: line.substring(colon + 1).replace(/^\s+|\s+$/g, "")
      };
      items.push(current);
    } else {
      current = null;
    }
  }
  return items;
}

function hMailServerRuleRebuildHeaderItems(items) {
  var output = "";
  for (var index = 0; index < items.length; index++) {
    if (String(items[index].Value || "").length > 0) {
      output = hMailServerRuleAppendHeaderLine(output, hMailServerRuleCanonicalHeaderName(items[index].Name) + ": " + hMailServerRuleSanitizeHeaderValue(items[index].Value));
    }
  }
  return output;
}

function hMailServerRuleSetHeaderAt(headers, index, fieldName, fieldValue) {
  var items = hMailServerRuleParseHeaderItems(headers);
  if (index < 0 || index >= items.length) {
    return headers;
  }
  items[index] = {
    Name: String(fieldName || ""),
    Value: String(fieldValue || "")
  };
  return hMailServerRuleRebuildHeaderItems(items);
}

function hMailServerRuleDeleteHeaderAt(headers, index) {
  var items = hMailServerRuleParseHeaderItems(headers);
  if (index < 0 || index >= items.length) {
    return headers;
  }
  items.splice(index, 1);
  return hMailServerRuleRebuildHeaderItems(items);
}

function hMailServerRuleSyncMessageHeaderFields(message) {
  message.Subject = message.HeaderValue("Subject");
  message.From = message.HeaderValue("From");
  message._to = message.HeaderValue("To");
  message.To = message._to;
  message._cc = message.HeaderValue("Cc") || message.HeaderValue("CC");
  message.CC = message._cc;
  message.Date = message.HeaderValue("Date");
  message.Charset = hMailServerRuleExtractCharset(message.HeaderValue("Content-Type"));
  if (message.Headers) {
    message.Headers._refresh();
  }
}

function hMailServerRuleSyncCommonHeaderField(message, fieldName, fieldValue) {
  var name = String(fieldName || "").toLowerCase();
  var value = String(fieldValue || "");
  if (name === "subject") {
    message.Subject = value;
  } else if (name === "from") {
    message.From = value;
  } else if (name === "to") {
    message._to = value;
    message.To = value;
  } else if (name === "cc") {
    message._cc = value;
    message.CC = value;
  } else if (name === "date") {
    message.Date = value;
  } else if (name === "content-type") {
    message.Charset = hMailServerRuleExtractCharset(value);
  }
  if (message.Headers) {
    message.Headers._refresh();
  }
}

function hMailServerRuleCreateHeader(owner, collection, index, name, value) {
  var header = {
    Name: String(name || ""),
    Value: String(value || ""),
    _index: index,
    _deleted: false,
    Delete: function() {
      this._deleted = true;
    }
  };
  collection._issued.push(header);
  return header;
}

function hMailServerRuleCreateHeaders(owner) {
  return {
    _owner: owner,
    _items: [],
    _issued: [],
    Count: 0,
    _refresh: function() {
      this._items = hMailServerRuleParseHeaderItems(this._owner._headers);
      this.Count = this._items.length;
    },
    Item: function(index) {
      this._refresh();
      if (index < 0 || index >= this._items.length) {
        return null;
      }
      var item = this._items[index];
      return hMailServerRuleCreateHeader(this._owner, this, index, item.Name, item.Value);
    },
    ItemByName: function(name) {
      this._refresh();
      var target = String(name || "").toLowerCase();
      for (var index = 0; index < this._items.length; index++) {
        if (String(this._items[index].Name || "").toLowerCase() === target) {
          return hMailServerRuleCreateHeader(this._owner, this, index, this._items[index].Name, this._items[index].Value);
        }
      }
      return null;
    },
    _commit: function() {
      if (this._issued.length === 0) {
        this._refresh();
        return;
      }
      for (var index = 0; index < this._issued.length; index++) {
        var header = this._issued[index];
        if (header._deleted) {
          this._owner._headers = hMailServerRuleDeleteHeaderAt(this._owner._headers, header._index);
        } else {
          this._owner._headers = hMailServerRuleSetHeaderAt(this._owner._headers, header._index, header.Name, header.Value);
        }
      }
      this._issued = [];
      this._refresh();
      hMailServerRuleSyncMessageHeaderFields(this._owner);
    }
  };
}

function hMailServerRuleCreateRecipients() {
  return {
    _items: [],
    Count: 0,
    Item: function(index) {
      if (index < 0 || index >= this._items.length) {
        return null;
      }
      var item = this._items[index];
      return {
        Address: String(item.Address || ""),
        OriginalAddress: String(item.OriginalAddress || ""),
        IsLocalUser: Boolean(item.IsLocalUser)
      };
    },
    _append: function(address, originalAddress, isLocalUser) {
      this._items.push({
        Address: String(address || ""),
        OriginalAddress: String(originalAddress || address || ""),
        IsLocalUser: Boolean(isLocalUser)
      });
      this.Count = this._items.length;
    },
    _clear: function() {
      this._items = [];
      this.Count = 0;
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

function hMailServerRuleCleanMimeContentType(contentType) {
  var mediaType = String(contentType || "").split(";")[0].replace(/^\s+|\s+$/g, "").toLowerCase();
  return mediaType || "text/plain";
}

function hMailServerRuleExtractMimeBoundary(contentType) {
  var text = String(contentType || "");
  var inQuotes = false;
  var parameterStart = 0;
  for (var index = 0; index <= text.length; index++) {
    var character = index < text.length ? text.charAt(index) : ";";
    if (character === "\"") {
      inQuotes = !inQuotes;
    }
    if (character === ";" && !inQuotes) {
      var parameter = text.substring(parameterStart, index).replace(/^\s+|\s+$/g, "");
      var equalsPosition = parameter.indexOf("=");
      if (equalsPosition > 0 && parameter.substring(0, equalsPosition).replace(/^\s+|\s+$/g, "").toLowerCase() === "boundary") {
        var value = parameter.substring(equalsPosition + 1).replace(/^\s+|\s+$/g, "");
        if (value.length >= 2 && value.charAt(0) === "\"" && value.charAt(value.length - 1) === "\"") {
          value = value.substring(1, value.length - 1);
        }
        return value;
      }
      parameterStart = index + 1;
    }
  }
  return "";
}

function hMailServerRuleHasMimeBodyType(headers, body, bodyType, depth) {
  var requestedType = String(bodyType || "").toLowerCase();
  if (!requestedType) {
    return false;
  }

  var contentType = hMailServerRuleGetHeader(headers, "Content-Type");
  var mediaType = hMailServerRuleCleanMimeContentType(contentType);
  if (mediaType === requestedType) {
    return true;
  }
  if (depth >= 2 || mediaType.indexOf("multipart/") !== 0) {
    return false;
  }

  var boundary = hMailServerRuleExtractMimeBoundary(contentType);
  if (!boundary) {
    return false;
  }

  var marker = "\r\n--" + boundary;
  var parts = ("\r\n" + hMailServerRuleNormalizeLineBreaks(body)).split(marker);
  for (var index = 1; index < parts.length; index++) {
    var part = parts[index];
    if (part.substring(0, 2) === "--") {
      break;
    }
    if (part.substring(0, 2) === "\r\n") {
      part = part.substring(2);
    }
    var parsedPart = hMailServerRuleSplitMessage(part);
    if (hMailServerRuleHasMimeBodyType(parsedPart.headers, parsedPart.body, requestedType, depth + 1)) {
      return true;
    }
  }
  return false;
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
  return Math.floor(Number(hMailServerRuleFileSystem.GetFile(fileName).Size || 0) / 1024);
}

function hMailServerRuleAppendAttachmentOperation(operationPath, name, value, extraValue) {
  if (!operationPath) {
    return;
  }
  var operationFile = hMailServerRuleFileSystem.OpenTextFile(operationPath, 8, true);
  var operationLine = String(name || "") + "\t" + String(value || "").replace(/[\r\n]/g, " ");
  if (arguments.length > 3) {
    operationLine += "\t" + String(extraValue || "").replace(/[\r\n]/g, " ");
  }
  operationFile.WriteLine(operationLine);
  operationFile.Close();
}

function hMailServerRuleCreateAttachment(owner, index, fileName, size, sourcePath) {
  return {
    FileName: String(fileName || ""),
    Filename: String(fileName || ""),
    Size: Number(size || 0),
    SaveAs: function(path) {
      hMailServerRuleFileSystem.CopyFile(sourcePath, String(path || ""), true);
    },
    Delete: function() {
      owner._removeAt(index);
    }
  };
}

function hMailServerRuleCreateAttachments(manifestPath, operationPath) {
  var collection = {
    _items: [],
    Count: 0,
    _load: function() {
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
    _removeAt: function(index) {
      if (index < 0 || index >= this._items.length) {
        return;
      }
      hMailServerRuleAppendAttachmentOperation(operationPath, "DeleteIndex", String(index));
      this._items.splice(index, 1);
      this.Count = this._items.length;
    }
  };
  collection._load();
  return collection;
}

var HMAILSERVER_MESSAGE = null;
if ("{{hasMessageFlag}}" === "1") {
  HMAILSERVER_MESSAGE = {
    _fileName: "{{EscapeJScript(messagePath)}}",
    FileName: "{{EscapeJScript(messagePath)}}",
    Filename: "{{EscapeJScript(messagePath)}}",
    DropMessage: false,
    RejectReason: "",
    _id: 0,
    ID: 0,
    _uid: 0,
    UID: 0,
    _state: 0,
    State: 0,
    _flags: 0,
    _size: 0,
    Size: 0,
    _deliveryAttempt: 1,
    DeliveryAttempt: 1,
    _internalDate: new Date(),
    InternalDate: new Date(),
    Subject: "",
    From: "",
    FromAddress: "",
    _to: "",
    To: "",
    Recipients: hMailServerRuleCreateRecipients(),
    _cc: "",
    CC: "",
    Date: "",
    Charset: "",
    EncodeFields: true,
    Body: "",
    HTMLBody: "",
    Attachments: hMailServerRuleCreateAttachments(
      "{{EscapeJScript(attachmentManifestPath)}}",
      "{{EscapeJScript(attachmentOperationPath)}}"),
    Headers: null,
    _headers: "",
    _operationPath: "{{EscapeJScript(attachmentOperationPath)}}",
    _copySequence: 0,
    _restoreReadOnlyMetadata: function() {
      this.ID = this._id;
      this.UID = this._uid;
      this.State = this._state;
      this.Size = this._size;
      this.DeliveryAttempt = this._deliveryAttempt;
      this.InternalDate = new Date(this._internalDate.getTime());
    },
    Load: function() {
      var parsed = hMailServerRuleSplitMessage(hMailServerRuleReadAllText(this._fileName));
      this._headers = parsed.headers;
      this.Body = parsed.body;
      this._size = hMailServerRuleGetMessageSize(this._fileName);
      hMailServerRuleSyncMessageHeaderFields(this);
      this.HTMLBody = hMailServerRuleGetHeader(this._headers, "Content-Type").toLowerCase().indexOf("text/html") >= 0 ? this.Body : "";
      this._restoreReadOnlyMetadata();
    },
    RefreshContent: function() {
      this.Load();
    },
    Copy: function(destinationFolderID) {
      this._restoreReadOnlyMetadata();
      var folderID = Number(destinationFolderID);
      if (!isFinite(folderID) || Math.floor(folderID) !== folderID || folderID <= 0 || folderID > 2147483647) {
        throw new Error("Invalid destination folder ID.");
      }
      if (!this._operationPath) {
        throw new Error("Message copy operations are unavailable.");
      }
      var snapshotPath = this._operationPath + ".copy-" + String(this._copySequence) + ".eml";
      this._copySequence++;
      hMailServerRuleFileSystem.CopyFile(this._fileName, snapshotPath, true);
      hMailServerRuleAppendAttachmentOperation(this._operationPath, "CopyFolder", String(folderID), snapshotPath);
    },
    HeaderValue: function(fieldName) {
      return hMailServerRuleGetHeader(this._headers, fieldName);
    },
    SetHeaderValue: function(fieldName, fieldValue) {
      this._headers = hMailServerRuleSetHeader(this._headers, fieldName, fieldValue);
      hMailServerRuleSyncCommonHeaderField(this, fieldName, fieldValue);
    },
    Flag: function(flagValue, enabled) {
      this._restoreReadOnlyMetadata();
      var mask = Number(flagValue) || 0;
      if (arguments.length > 1) {
        this.SetFlag(mask, enabled);
      }
      return ((Number(this._flags) || 0) & mask) !== 0;
    },
    GetFlag: function(flagValue) {
      return this.Flag(flagValue);
    },
    SetFlag: function(flagValue, enabled) {
      this._restoreReadOnlyMetadata();
      var mask = Number(flagValue) || 0;
      var current = Number(this._flags) || 0;
      this._flags = enabled ? (current | mask) : (current & ~mask);
    },
    Save: function() {
      this._restoreReadOnlyMetadata();
      if (this.Headers) {
        this.Headers._commit();
      }
      var headers = this._headers;
      headers = hMailServerRuleSetHeader(headers, "Subject", this.Subject);
      headers = hMailServerRuleSetHeader(headers, "From", this.From);
      this.To = this._to;
      this.CC = this._cc;
      headers = hMailServerRuleSetHeader(headers, "To", this._to);
      headers = hMailServerRuleSetHeader(headers, "Cc", this._cc);
      headers = hMailServerRuleSetHeader(headers, "Date", this.Date);
      if (this.Charset) {
        headers = hMailServerRuleSetHeader(headers, "Content-Type", hMailServerRuleApplyCharset(hMailServerRuleGetHeader(headers, "Content-Type"), this.Charset));
      }
      var messageBody = this.Body;
      if (this.HTMLBody && hMailServerRuleGetHeader(headers, "Content-Type").toLowerCase().indexOf("text/html") >= 0) {
        messageBody = this.HTMLBody;
      }
      hMailServerRuleWriteAllText(this._fileName, headers + "\r\n\r\n" + messageBody);
      this._headers = headers;
      this._size = hMailServerRuleGetMessageSize(this._fileName);
      this._restoreReadOnlyMetadata();
    },
    AddRecipient: function(name, address) {
      this.Recipients._append(address, address, false);
      var displayAddress = hMailServerRuleFormatRecipientForHeader(name, address);
      this._to = this._to ? this._to + ", " + displayAddress : displayAddress;
      this.To = this._to;
    },
    ClearRecipients: function() {
      this.Recipients._clear();
      this._to = "";
      this._cc = "";
      this.To = this._to;
      this.CC = this._cc;
      this._headers = hMailServerRuleSetHeader(this._headers, "To", "");
      this._headers = hMailServerRuleSetHeader(this._headers, "Cc", "");
      this._headers = hMailServerRuleSetHeader(this._headers, "Bcc", "");
    },
    HasBodyType: function(bodyType) {
      return hMailServerRuleHasMimeBodyType(this._headers, this.Body, bodyType, 0);
    },
    SetCharset: function(charset) {
      this.Charset = String(charset || "");
      this._headers = hMailServerRuleSetHeader(this._headers, "Content-Type", hMailServerRuleApplyCharset(this.HeaderValue("Content-Type"), this.Charset));
    }
  };
  HMAILSERVER_MESSAGE.Headers = hMailServerRuleCreateHeaders(HMAILSERVER_MESSAGE);
  HMAILSERVER_MESSAGE.Load();
  HMAILSERVER_MESSAGE.FromAddress = "{{EscapeJScript(mailFrom)}}";
  HMAILSERVER_MESSAGE._id = {{messageMetadata.Id.ToString(CultureInfo.InvariantCulture)}};
  HMAILSERVER_MESSAGE._uid = {{messageMetadata.Uid.ToString(CultureInfo.InvariantCulture)}};
  HMAILSERVER_MESSAGE._state = {{messageMetadata.State.ToString(CultureInfo.InvariantCulture)}};
  HMAILSERVER_MESSAGE._flags = {{messageMetadata.Flags.ToString(CultureInfo.InvariantCulture)}};
  HMAILSERVER_MESSAGE._deliveryAttempt = {{messageMetadata.DeliveryAttempt.ToString(CultureInfo.InvariantCulture)}};
  HMAILSERVER_MESSAGE._internalDate = {{CreateJScriptUtcDateExpression(messageMetadata.InternalDateUtc)}};
  HMAILSERVER_MESSAGE._restoreReadOnlyMetadata();
{{CreateJScriptRecipientSeeds(recipients)}}
}

var HMAILSERVER_FETCHACCOUNT = {{CreateJScriptFetchAccountObject(fetchAccount)}};
var hMailServerRuleExternalAccountRemoteUid = "{{EscapeJScript(externalAccountRemoteUid)}}";

var hMailServerRuleDeliveryRecipient = "{{EscapeJScript(deliveryRecipientAddress)}}";
var hMailServerRuleDeliveryErrorMessage = "{{EscapeJScript(deliveryErrorMessage)}}";

var HMAILSERVER_CLIENT = {
  Username: "",
  IPAddress: "",
  Port: 0,
  SessionID: 0,
  HELO: "",
  IsAuthenticated: false,
  IsEncryptedConnection: false,
  Authenticated: false,
  EncryptedConnection: false,
  CipherVersion: "",
  CipherName: "",
  CipherBits: 0
};
{{CreateJScriptClientSeed(client)}}

var Result = {
  Value: 0,
  Parameter: 0,
  Message: ""
};

var hMailServerRuleScriptFile = hMailServerRuleFileSystem.OpenTextFile("{{EscapeJScript(scriptPath)}}", 1, false);
eval(hMailServerRuleScriptFile.ReadAll());
hMailServerRuleScriptFile.Close();
{{CreateJScriptInvocation(functionName, invocation, argumentShape)}}
var hMailServerRuleStatusFile = hMailServerRuleFileSystem.CreateTextFile("{{EscapeJScript(statusPath)}}", true, false);
if ("{{isDeliveryEvent}}" === "1" && "{{hasMessageFlag}}" === "1" && Result.Value === 1) {
  HMAILSERVER_MESSAGE.DropMessage = true;
}
hMailServerRuleStatusFile.WriteLine("{{hasMessageFlag}}" === "1" && HMAILSERVER_MESSAGE.DropMessage ? "DropMessage=1" : "DropMessage=0");
var hMailServerRuleRejectReason = "";
if ("{{usesSmtpRejectResult}}" === "1") {
  if ("{{hasMessageFlag}}" === "1") {
    hMailServerRuleRejectReason = String(HMAILSERVER_MESSAGE.RejectReason || "");
  }
  if (Result.Value === 1) {
    hMailServerRuleRejectReason = "554 Rejected";
  } else if (Result.Value === 2) {
    hMailServerRuleRejectReason = "554 " + String(Result.Message || "");
  } else if (Result.Value === 3) {
    hMailServerRuleRejectReason = "453 " + String(Result.Message || "");
  }
}
hMailServerRuleStatusFile.WriteLine("RejectReason=" + hMailServerRuleRejectReason.replace(/[\r\n]/g, " "));
hMailServerRuleStatusFile.WriteLine("ResultValue=" + String(Result.Value));
hMailServerRuleStatusFile.WriteLine("ResultParameter=" + String(Result.Parameter || 0));
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
            builder.Append("HMAILSERVER_MESSAGE.Recipients.AppendInternal \"")
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
            builder.Append("HMAILSERVER_MESSAGE.Recipients._append(\"")
                .Append(EscapeJScript(recipient.Address))
                .Append("\", \"")
                .Append(EscapeJScript(recipient.OriginalAddress))
                .Append("\", ")
                .Append(recipient.IsLocal ? "true" : "false")
                .AppendLine(");");
        }

        return builder.ToString();
    }

    private static string CreateVbScriptFetchAccountSeed(ExternalFetchAccountLease? account)
    {
        var connectionSecurity = account?.ConnectionSecurity ?? ExternalFetchConnectionSecurity.None;
        var builder = new StringBuilder();
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "ID", account?.FetchAccountId ?? 0);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "AccountID", account?.AccountId ?? 0);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "Name", account?.Name ?? string.Empty);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "ServerAddress", account?.ServerAddress ?? string.Empty);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "Port", account?.ServerPort ?? 0);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "ServerType", (int)(account?.ServerType ?? ExternalFetchServerType.Pop3));
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "Username", account?.Username ?? string.Empty);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "Password", account?.Password ?? string.Empty);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "MinutesBetweenFetch", account?.MinutesBetweenFetch ?? 0);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "DaysToKeepMessages", account?.DaysToKeep ?? 0);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "Enabled", account is not null);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "MIMERecipientHeaders", account?.MimeRecipientHeaders ?? string.Empty);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "ProcessMIMERecipients", account?.ProcessMimeRecipients ?? false);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "ProcessMIMEDate", account?.ProcessMimeDate ?? false);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "UseSSL", connectionSecurity == ExternalFetchConnectionSecurity.Ssl);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "ConnectionSecurity", (int)connectionSecurity);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "UseAntiSpam", account?.UseAntiSpam ?? false);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "UseAntiVirus", account?.UseAntiVirus ?? false);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "EnableRouteRecipients", account?.EnableRouteRecipients ?? false);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "NextDownloadTime", account?.NextDownloadTime ?? string.Empty);
        AppendVbScriptObjectAssignment(builder, "HMAILSERVER_FETCHACCOUNT", "IsLocked", account?.IsLocked ?? false);
        return builder.ToString();
    }

    private static string CreateJScriptFetchAccountObject(ExternalFetchAccountLease? account)
    {
        var connectionSecurity = account?.ConnectionSecurity ?? ExternalFetchConnectionSecurity.None;
        string[] properties =
        [
            $"  ID: {ToInvariant(account?.FetchAccountId ?? 0)}",
            $"  AccountID: {ToInvariant(account?.AccountId ?? 0)}",
            $"  Name: \"{EscapeJScript(account?.Name ?? string.Empty)}\"",
            $"  ServerAddress: \"{EscapeJScript(account?.ServerAddress ?? string.Empty)}\"",
            $"  Port: {ToInvariant(account?.ServerPort ?? 0)}",
            $"  ServerType: {ToInvariant((int)(account?.ServerType ?? ExternalFetchServerType.Pop3))}",
            $"  Username: \"{EscapeJScript(account?.Username ?? string.Empty)}\"",
            $"  Password: \"{EscapeJScript(account?.Password ?? string.Empty)}\"",
            $"  MinutesBetweenFetch: {ToInvariant(account?.MinutesBetweenFetch ?? 0)}",
            $"  DaysToKeepMessages: {ToInvariant(account?.DaysToKeep ?? 0)}",
            $"  Enabled: {(account is not null ? "true" : "false")}",
            $"  MIMERecipientHeaders: \"{EscapeJScript(account?.MimeRecipientHeaders ?? string.Empty)}\"",
            $"  ProcessMIMERecipients: {ToJScriptBoolean(account?.ProcessMimeRecipients ?? false)}",
            $"  ProcessMIMEDate: {ToJScriptBoolean(account?.ProcessMimeDate ?? false)}",
            $"  UseSSL: {ToJScriptBoolean(connectionSecurity == ExternalFetchConnectionSecurity.Ssl)}",
            $"  ConnectionSecurity: {ToInvariant((int)connectionSecurity)}",
            $"  UseAntiSpam: {ToJScriptBoolean(account?.UseAntiSpam ?? false)}",
            $"  UseAntiVirus: {ToJScriptBoolean(account?.UseAntiVirus ?? false)}",
            $"  EnableRouteRecipients: {ToJScriptBoolean(account?.EnableRouteRecipients ?? false)}",
            $"  NextDownloadTime: \"{EscapeJScript(account?.NextDownloadTime ?? string.Empty)}\"",
            $"  IsLocked: {ToJScriptBoolean(account?.IsLocked ?? false)}"
        ];

        return "{" + Environment.NewLine +
            string.Join("," + Environment.NewLine, properties) +
            Environment.NewLine + "}";
    }

    private static string CreateVbScriptClientSeed(SmtpEventScriptClient? client)
    {
        var builder = new StringBuilder();
        AppendVbScriptAssignment(builder, "Username", client?.Username ?? string.Empty);
        AppendVbScriptAssignment(builder, "IPAddress", client?.IPAddress ?? string.Empty);
        AppendVbScriptAssignment(builder, "Port", client?.Port ?? 0);
        AppendVbScriptAssignment(builder, "SessionID", client?.SessionId ?? 0);
        AppendVbScriptAssignment(builder, "HELO", client?.HeloHost ?? string.Empty);
        AppendVbScriptAssignment(builder, "IsAuthenticated", client?.IsAuthenticated ?? false);
        AppendVbScriptAssignment(builder, "IsEncryptedConnection", client?.IsEncryptedConnection ?? false);
        AppendVbScriptAssignment(builder, "Authenticated", client?.IsAuthenticated ?? false);
        AppendVbScriptAssignment(builder, "EncryptedConnection", client?.IsEncryptedConnection ?? false);
        AppendVbScriptAssignment(builder, "CipherVersion", client?.CipherVersion ?? string.Empty);
        AppendVbScriptAssignment(builder, "CipherName", client?.CipherName ?? string.Empty);
        AppendVbScriptAssignment(builder, "CipherBits", client?.CipherBits ?? 0);
        return builder.ToString();
    }

    private static string CreateJScriptClientSeed(SmtpEventScriptClient? client)
    {
        var builder = new StringBuilder();
        AppendJScriptAssignment(builder, "Username", client?.Username ?? string.Empty);
        AppendJScriptAssignment(builder, "IPAddress", client?.IPAddress ?? string.Empty);
        AppendJScriptAssignment(builder, "Port", client?.Port ?? 0);
        AppendJScriptAssignment(builder, "SessionID", client?.SessionId ?? 0);
        AppendJScriptAssignment(builder, "HELO", client?.HeloHost ?? string.Empty);
        AppendJScriptAssignment(builder, "IsAuthenticated", client?.IsAuthenticated ?? false);
        AppendJScriptAssignment(builder, "IsEncryptedConnection", client?.IsEncryptedConnection ?? false);
        AppendJScriptAssignment(builder, "Authenticated", client?.IsAuthenticated ?? false);
        AppendJScriptAssignment(builder, "EncryptedConnection", client?.IsEncryptedConnection ?? false);
        AppendJScriptAssignment(builder, "CipherVersion", client?.CipherVersion ?? string.Empty);
        AppendJScriptAssignment(builder, "CipherName", client?.CipherName ?? string.Empty);
        AppendJScriptAssignment(builder, "CipherBits", client?.CipherBits ?? 0);
        return builder.ToString();
    }

    private static string CreateVbScriptInvocation(
        string functionName,
        ScriptInvocation invocation,
        ScriptArgumentShape argumentShape) =>
        invocation == ScriptInvocation.RuleFunction
            ? $"Call {functionName}(HMAILSERVER_MESSAGE)"
            : string.Join(
                Environment.NewLine,
                "Dim hMailServerEventHandler",
                "On Error Resume Next",
                $"Set hMailServerEventHandler = GetRef(\"{EscapeVbScript(functionName)}\")",
                "If Err.Number <> 0 Then",
                "   Err.Clear",
                "   On Error GoTo 0",
                "Else",
                "   On Error GoTo 0",
                CreateVbScriptOptionalEventCall(argumentShape),
                "End If");

    private static string CreateJScriptInvocation(
        string functionName,
        ScriptInvocation invocation,
        ScriptArgumentShape argumentShape)
    {
        return invocation == ScriptInvocation.RuleFunction
            ? $$"""
if (typeof {{functionName}} !== "function") {
  throw new Error("Script function not found: {{EscapeJScript(functionName)}}");
}
{{functionName}}(HMAILSERVER_MESSAGE);
"""
            : argumentShape == ScriptArgumentShape.ClientOnly
                ? $$"""
if (typeof {{functionName}} === "function") {
  {{functionName}}(HMAILSERVER_CLIENT);
}
"""
            : argumentShape == ScriptArgumentShape.ClientAndMessage
                ? $$"""
if (typeof {{functionName}} === "function") {
  {{functionName}}(HMAILSERVER_CLIENT, HMAILSERVER_MESSAGE);
}
"""
                : argumentShape == ScriptArgumentShape.MessageRecipientAndError
                    ? $$"""
if (typeof {{functionName}} === "function") {
  {{functionName}}(HMAILSERVER_MESSAGE, hMailServerRuleDeliveryRecipient, hMailServerRuleDeliveryErrorMessage);
}
"""
                    : argumentShape == ScriptArgumentShape.FetchAccountMessageAndUid
                        ? $$"""
if (typeof {{functionName}} === "function") {
  {{functionName}}(HMAILSERVER_FETCHACCOUNT, HMAILSERVER_MESSAGE, hMailServerRuleExternalAccountRemoteUid);
}
"""
                    : $$"""
if (typeof {{functionName}} === "function") {
  {{functionName}}(HMAILSERVER_MESSAGE);
}
""";
    }

    private static string CreateVbScriptOptionalEventCall(ScriptArgumentShape argumentShape) =>
        argumentShape switch
        {
            ScriptArgumentShape.ClientOnly => "   Call hMailServerEventHandler(HMAILSERVER_CLIENT)",
            ScriptArgumentShape.ClientAndMessage => "   Call hMailServerEventHandler(HMAILSERVER_CLIENT, HMAILSERVER_MESSAGE)",
            ScriptArgumentShape.MessageRecipientAndError => "   Call hMailServerEventHandler(HMAILSERVER_MESSAGE, hMailServerRuleDeliveryRecipient, hMailServerRuleDeliveryErrorMessage)",
            ScriptArgumentShape.FetchAccountMessageAndUid => "   Call hMailServerEventHandler(HMAILSERVER_FETCHACCOUNT, HMAILSERVER_MESSAGE, hMailServerRuleExternalAccountRemoteUid)",
            _ => "   Call hMailServerEventHandler(HMAILSERVER_MESSAGE)"
        };

    private static void AppendVbScriptAssignment(StringBuilder builder, string name, string value)
    {
        builder.Append("HMAILSERVER_CLIENT.")
            .Append(name)
            .Append(" = \"")
            .Append(EscapeVbScript(value))
            .AppendLine("\"");
    }

    private static void AppendVbScriptAssignment(StringBuilder builder, string name, int value) =>
        builder.Append("HMAILSERVER_CLIENT.")
            .Append(name)
            .Append(" = ")
            .AppendLine(value.ToString(CultureInfo.InvariantCulture));

    private static void AppendVbScriptAssignment(StringBuilder builder, string name, long value) =>
        builder.Append("HMAILSERVER_CLIENT.")
            .Append(name)
            .Append(" = ")
            .AppendLine(value.ToString(CultureInfo.InvariantCulture));

    private static void AppendVbScriptAssignment(StringBuilder builder, string name, bool value) =>
        builder.Append("HMAILSERVER_CLIENT.")
            .Append(name)
            .Append(" = ")
            .AppendLine(value ? "True" : "False");

    private static void AppendVbScriptObjectAssignment(StringBuilder builder, string objectName, string name, string value)
    {
        builder.Append(objectName)
            .Append('.')
            .Append(name)
            .Append(" = \"")
            .Append(EscapeVbScript(value))
            .AppendLine("\"");
    }

    private static void AppendVbScriptObjectAssignment(StringBuilder builder, string objectName, string name, int value) =>
        builder.Append(objectName)
            .Append('.')
            .Append(name)
            .Append(" = ")
            .AppendLine(ToInvariant(value));

    private static void AppendVbScriptObjectAssignment(StringBuilder builder, string objectName, string name, bool value) =>
        builder.Append(objectName)
            .Append('.')
            .Append(name)
            .Append(" = ")
            .AppendLine(value ? "True" : "False");

    private static void AppendJScriptAssignment(StringBuilder builder, string name, string value)
    {
        builder.Append("HMAILSERVER_CLIENT.")
            .Append(name)
            .Append(" = \"")
            .Append(EscapeJScript(value))
            .AppendLine("\";");
    }

    private static void AppendJScriptAssignment(StringBuilder builder, string name, int value) =>
        builder.Append("HMAILSERVER_CLIENT.")
            .Append(name)
            .Append(" = ")
            .Append(value.ToString(CultureInfo.InvariantCulture))
            .AppendLine(";");

    private static void AppendJScriptAssignment(StringBuilder builder, string name, long value) =>
        builder.Append("HMAILSERVER_CLIENT.")
            .Append(name)
            .Append(" = ")
            .Append(value.ToString(CultureInfo.InvariantCulture))
            .AppendLine(";");

    private static void AppendJScriptAssignment(StringBuilder builder, string name, bool value) =>
        builder.Append("HMAILSERVER_CLIENT.")
            .Append(name)
            .Append(" = ")
            .Append(value ? "true" : "false")
            .AppendLine(";");

    private static string ToInvariant(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string ToJScriptBoolean(bool value) =>
        value ? "true" : "false";

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

    private static ScriptArgumentShape ToScriptArgumentShape(SmtpEventScriptArgumentShape argumentShape) =>
        argumentShape == SmtpEventScriptArgumentShape.ClientOnly
            ? ScriptArgumentShape.ClientOnly
            : ScriptArgumentShape.ClientAndMessage;

    private static ScriptArgumentShape ToScriptArgumentShape(DeliveryEventScriptArgumentShape argumentShape) =>
        argumentShape == DeliveryEventScriptArgumentShape.MessageRecipientAndError
            ? ScriptArgumentShape.MessageRecipientAndError
            : ScriptArgumentShape.MessageOnly;

    private static string EscapeVbScript(string value) =>
        value.Replace("\"", "\"\"", StringComparison.Ordinal)
            .Replace("\r\n", "\" & vbCrLf & \"", StringComparison.Ordinal)
            .Replace("\r", "\" & vbCr & \"", StringComparison.Ordinal)
            .Replace("\n", "\" & vbLf & \"", StringComparison.Ordinal);

    private static string EscapeJScript(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string CreateVbScriptDateExpression(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"DateSerial({utc.Year}, {utc.Month}, {utc.Day}) + TimeSerial({utc.Hour}, {utc.Minute}, {utc.Second})");
    }

    private static string CreateJScriptUtcDateExpression(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"new Date(Date.UTC({utc.Year}, {utc.Month - 1}, {utc.Day}, {utc.Hour}, {utc.Minute}, {utc.Second}))");
    }

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

    private enum ScriptInvocation
    {
        RuleFunction,
        OptionalSmtpEvent,
        OptionalDeliveryEvent,
        OptionalExternalAccountDownload
    }

    private enum ScriptArgumentShape
    {
        ClientOnly,
        ClientAndMessage,
        MessageOnly,
        MessageRecipientAndError,
        FetchAccountMessageAndUid
    }

    private sealed record ScriptExecutionSpec(
        string FunctionName,
        string MailFrom,
        IReadOnlyList<SmtpResolvedRecipient> Recipients,
        byte[]? MessageData,
        SmtpEventScriptClient? Client,
        ScriptInvocation Invocation,
        ScriptArgumentShape ArgumentShape,
        string DeliveryRecipientAddress,
        string DeliveryErrorMessage,
        ScriptMessageMetadata MessageMetadata,
        ExternalFetchAccountLease? FetchAccount = null,
        string ExternalAccountRemoteUid = "");

    private sealed record ScriptMessageMetadata(
        long Id,
        long Uid,
        int State,
        int Flags,
        int DeliveryAttempt,
        DateTimeOffset InternalDateUtc);

    private sealed record ScriptLanguage(string Name, string Extension);

    private sealed record ScriptStatus(
        bool Found,
        bool DropMessage,
        string RejectReason,
        int ResultValue,
        int ResultParameter);

    private sealed record ProcessResult(bool Succeeded, string Error);
}
