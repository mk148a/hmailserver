using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HMailServer.Core.Abstractions;

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
            var runnerPath = Path.Combine(tempDirectory, language.Extension == "vbs" ? "runner.vbs" : "runner.js");
            File.WriteAllBytes(messagePath, request.MessageData);
            File.WriteAllText(
                runnerPath,
                language.Extension == "vbs"
                    ? CreateVbScriptRunner(scriptPath, request.FunctionName, messagePath, statusPath)
                    : CreateJScriptRunner(scriptPath, request.FunctionName, messagePath, statusPath),
                Encoding.Unicode);

            var processResult = RunScript(runnerPath, cancellationToken);
            if (!processResult.Succeeded)
            {
                return SmtpRuleScriptExecutionResult.Failure(processResult.Error);
            }

            var status = ReadStatus(statusPath);
            var messageData = File.Exists(messagePath)
                ? File.ReadAllBytes(messagePath)
                : request.MessageData;
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
            return new ScriptStatus(false, string.Empty);
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

        return new ScriptStatus(dropMessage, rejectReason);
    }

    private static string CreateVbScriptRunner(
        string scriptPath,
        string functionName,
        string messagePath,
        string statusPath)
    {
        return $$"""
ExecuteGlobal CreateObject("Scripting.FileSystemObject").OpenTextFile("{{EscapeVbScript(scriptPath)}}", 1, False).ReadAll

Class HMailServerRuleMessage
   Public FileName
   Public DropMessage
   Public RejectReason
End Class

Dim HMAILSERVER_MESSAGE
Set HMAILSERVER_MESSAGE = New HMailServerRuleMessage
HMAILSERVER_MESSAGE.FileName = "{{EscapeVbScript(messagePath)}}"
HMAILSERVER_MESSAGE.DropMessage = False
HMAILSERVER_MESSAGE.RejectReason = ""

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
        string statusPath)
    {
        return $$"""
var HMAILSERVER_MESSAGE = {
  FileName: "{{EscapeJScript(messagePath)}}",
  DropMessage: false,
  RejectReason: ""
};
var hMailServerRuleFileSystem = new ActiveXObject("Scripting.FileSystemObject");
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

    private sealed record ScriptStatus(bool DropMessage, string RejectReason);

    private sealed record ProcessResult(bool Succeeded, string Error);
}
