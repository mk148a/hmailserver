using System.Diagnostics;
using System.Globalization;
using HMailServer.Core.Abstractions;

namespace HMailServer.Scripting;

public sealed class WindowsScriptSyntaxChecker : IScriptSyntaxChecker
{
    private readonly WindowsScriptRuleExecutorOptions _options;

    public WindowsScriptSyntaxChecker(WindowsScriptRuleExecutorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.Timeout.Ticks, 0);
        _options = options;
    }

    public string CheckSyntax(string language, string scriptFile)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptFile);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows Script Host is only available on Windows.");
        }

        string contents;
        try
        {
            contents = File.ReadAllText(scriptFile);
        }
        catch (FileNotFoundException)
        {
            return string.Empty;
        }
        catch (DirectoryNotFoundException)
        {
            return string.Empty;
        }

        if (contents.Length == 0)
        {
            return string.Empty;
        }

        using var process = new Process
        {
            StartInfo = CreateStartInfo(language, scriptFile)
        };

        process.Start();
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(_options.Timeout))
        {
            TryKill(process);
            process.WaitForExit();
            return $"File: {scriptFile}\r\nScript syntax check timed out.";
        }

        process.WaitForExit();
        var output = standardOutput.GetAwaiter().GetResult();
        var error = standardError.GetAwaiter().GetResult();
        if (process.ExitCode == 0)
        {
            return string.Empty;
        }

        var details = string.Concat(error, output).Trim();
        if (details.Length == 0)
        {
            details = "Windows Script Host failed with exit code " +
                process.ExitCode.ToString(CultureInfo.InvariantCulture) + ".";
        }

        return $"File: {scriptFile}\r\n{details}";
    }

    private ProcessStartInfo CreateStartInfo(string language, string scriptFile)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.CScriptPath,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.ArgumentList.Add("//NoLogo");
        if (language is "VBScript" or "JScript")
        {
            startInfo.ArgumentList.Add("//E:" + language);
        }

        startInfo.ArgumentList.Add(scriptFile);
        return startInfo;
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }
}
