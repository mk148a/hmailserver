using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed record CustomScannerTestRuntimeOptions
{
    public string DataDirectory { get; init; } = Path.GetTempPath();

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(20);
}

internal delegate CustomScannerProcessResult CustomScannerProcessRunner(
    ProcessStartInfo startInfo,
    TimeSpan timeout);

internal sealed record CustomScannerProcessResult(
    bool Started,
    int ExitCode,
    string ResultText = "");

public sealed class CustomScannerTestRuntime : ICustomScannerTestRuntime
{
    private static readonly byte[] CleanTestMessage = "Test"u8.ToArray();

    private const string LaunchFailureText = "Unable to launch executable.";
    private const string ReversedEicarTestString =
        " *H+H$!ELIF-TSET-SURIVITNA-DRADNATS-RACIE$}7)CC7)^P(45XZP\\4[PA@%P!O5X";
    private const string TimeoutText = "Timed out waiting for custom scanner.";

    private readonly CustomScannerTestRuntimeOptions _options;
    private readonly CustomScannerProcessRunner _processRunner;

    public CustomScannerTestRuntime(CustomScannerTestRuntimeOptions? options = null)
        : this(options, RunProcess)
    {
    }

    internal CustomScannerTestRuntime(
        CustomScannerTestRuntimeOptions? options,
        CustomScannerProcessRunner processRunner)
    {
        _options = options ?? new CustomScannerTestRuntimeOptions();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_options.Timeout.Ticks, 0);
        _processRunner = processRunner;
    }

    public CustomScannerTestResult TestConnection(
        string commandLineTemplate,
        int virusReturnCode)
    {
        try
        {
            var cleanFile = WriteTestFile(CleanTestMessage);
            try
            {
                var cleanResult = ScanFile(commandLineTemplate, virusReturnCode, cleanFile);
                if (cleanResult.VirusFound)
                {
                    return new CustomScannerTestResult(
                        false,
                        "False positive: " + cleanResult.Details);
                }
            }
            finally
            {
                DeleteIfExists(cleanFile);
            }

            var eicarFile = WriteTestFile(CreateEicarTestMessage());
            try
            {
                var eicarResult = ScanFile(commandLineTemplate, virusReturnCode, eicarFile);
                return new CustomScannerTestResult(
                    eicarResult.VirusFound,
                    eicarResult.Details);
            }
            finally
            {
                DeleteIfExists(eicarFile);
            }
        }
        catch (Exception ex) when (IsHandledRuntimeException(ex))
        {
            return new CustomScannerTestResult(false, ex.Message);
        }
    }

    internal static ProcessStartInfo CreateStartInfo(
        string commandLineTemplate,
        string filePath)
    {
        var fullFilePath = Path.GetFullPath(filePath);
        var workingDirectory = Path.GetDirectoryName(fullFilePath);
        if (string.IsNullOrEmpty(workingDirectory))
        {
            throw new ArgumentException("The scan file path must include a directory.", nameof(filePath));
        }

        var commandLine = BuildCommandLine(commandLineTemplate ?? string.Empty, fullFilePath);
        var arguments = SplitWindowsCommandLine(commandLine);
        if (arguments.Count == 0)
        {
            throw new ArgumentException("The custom scanner command line must include an executable path.", nameof(commandLineTemplate));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = arguments[0],
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        for (var index = 1; index < arguments.Count; index++)
        {
            startInfo.ArgumentList.Add(arguments[index]);
        }

        return startInfo;
    }

    internal static string BuildCommandLine(
        string commandLineTemplate,
        string filePath)
    {
        var quotedFile = QuoteWindowsCommandLineArgument(filePath);
        if (commandLineTemplate.Contains("\"%FILE%\"", StringComparison.Ordinal))
        {
            return commandLineTemplate.Replace("\"%FILE%\"", quotedFile, StringComparison.Ordinal);
        }

        return commandLineTemplate.Contains("%FILE%", StringComparison.Ordinal)
            ? commandLineTemplate.Replace("%FILE%", quotedFile, StringComparison.Ordinal)
            : commandLineTemplate + " " + quotedFile;
    }

    internal static string QuoteWindowsCommandLineArgument(string argument)
    {
        var quoted = new StringBuilder("\"");
        var backslashCount = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                quoted.Append('\\', backslashCount * 2 + 1);
                quoted.Append(character);
            }
            else
            {
                quoted.Append('\\', backslashCount);
                quoted.Append(character);
            }

            backslashCount = 0;
        }

        quoted.Append('\\', backslashCount * 2);
        quoted.Append('"');
        return quoted.ToString();
    }

    internal static IReadOnlyList<string> SplitWindowsCommandLine(string commandLine)
    {
        var arguments = new List<string>();
        var index = 0;
        while (true)
        {
            while (index < commandLine.Length && char.IsWhiteSpace(commandLine[index]))
            {
                index++;
            }

            if (index >= commandLine.Length)
            {
                return arguments;
            }

            var argument = new StringBuilder();
            var inQuotes = false;
            while (index < commandLine.Length)
            {
                var backslashCount = 0;
                while (index < commandLine.Length && commandLine[index] == '\\')
                {
                    backslashCount++;
                    index++;
                }

                if (index < commandLine.Length && commandLine[index] == '"')
                {
                    argument.Append('\\', backslashCount / 2);
                    if (backslashCount % 2 == 0)
                    {
                        inQuotes = !inQuotes;
                    }
                    else
                    {
                        argument.Append('"');
                    }

                    index++;
                    continue;
                }

                argument.Append('\\', backslashCount);
                if (index >= commandLine.Length)
                {
                    break;
                }

                var character = commandLine[index];
                if (!inQuotes && char.IsWhiteSpace(character))
                {
                    break;
                }

                argument.Append(character);
                index++;
            }

            arguments.Add(argument.ToString());
        }
    }

    private static CustomScannerProcessResult RunProcess(
        ProcessStartInfo startInfo,
        TimeSpan timeout)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = startInfo
            };
            if (!process.Start())
            {
                return new CustomScannerProcessResult(false, 0, LaunchFailureText);
            }

            if (!process.WaitForExit(timeout))
            {
                TryKill(process);
                return new CustomScannerProcessResult(false, 0, TimeoutText);
            }

            return new CustomScannerProcessResult(true, process.ExitCode);
        }
        catch (Exception ex) when (
            ex is Win32Exception
                or FileNotFoundException
                or InvalidOperationException
                or PlatformNotSupportedException)
        {
            return new CustomScannerProcessResult(false, 0, LaunchFailureText);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
                or Win32Exception
                or NotSupportedException)
        {
        }
    }

    private static bool IsHandledRuntimeException(Exception ex) =>
        ex is ArgumentException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException;

    private CustomScannerScanResult ScanFile(
        string commandLineTemplate,
        int virusReturnCode,
        string filePath)
    {
        var startInfo = CreateStartInfo(commandLineTemplate, filePath);
        var processResult = _processRunner(startInfo, _options.Timeout);
        if (!processResult.Started)
        {
            return new CustomScannerScanResult(
                false,
                string.IsNullOrEmpty(processResult.ResultText)
                    ? LaunchFailureText
                    : processResult.ResultText);
        }

        return processResult.ExitCode == virusReturnCode
            ? new CustomScannerScanResult(true, "Unknown")
            : new CustomScannerScanResult(false, "Return code: " + processResult.ExitCode);
    }

    private string WriteTestFile(byte[] contents)
    {
        var dataDirectory = NormalizeDirectory(_options.DataDirectory);
        Directory.CreateDirectory(dataDirectory);
        var filePath = Path.Combine(dataDirectory, Guid.NewGuid().ToString("N") + ".eml");
        File.WriteAllBytes(filePath, contents);
        return filePath;
    }

    private static string NormalizeDirectory(string directory) =>
        string.IsNullOrWhiteSpace(directory)
            ? Path.GetTempPath()
            : directory;

    private static byte[] CreateEicarTestMessage()
    {
        var characters = ReversedEicarTestString.ToCharArray();
        Array.Reverse(characters);
        return Encoding.ASCII.GetBytes(characters);
    }

    private static void DeleteIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private sealed record CustomScannerScanResult(
        bool VirusFound,
        string Details);
}
