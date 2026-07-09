using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed record ClamWinScannerTestRuntimeOptions
{
    public string DataDirectory { get; init; } = Path.GetTempPath();

    public string TempDirectory { get; init; } = Path.GetTempPath();

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(20);
}

internal delegate ClamWinScannerProcessResult ClamWinScannerProcessRunner(
    ProcessStartInfo startInfo,
    TimeSpan timeout);

internal sealed record ClamWinScannerProcessResult(
    bool Started,
    int ExitCode,
    string ResultText = "");

public sealed class ClamWinScannerTestRuntime : IClamWinScannerTestRuntime
{
    private static readonly byte[] CleanTestMessage = "Test"u8.ToArray();

    private const string LaunchFailureText = "Unable to launch executable.";
    private const string ReversedEicarTestString =
        " *H+H$!ELIF-TSET-SURIVITNA-DRADNATS-RACIE$}7)CC7)^P(45XZP\\4[PA@%P!O5X";
    private const string TimeoutText = "Timed out waiting for ClamWin scanner.";

    private readonly ClamWinScannerTestRuntimeOptions _options;
    private readonly ClamWinScannerProcessRunner _processRunner;

    public ClamWinScannerTestRuntime(ClamWinScannerTestRuntimeOptions? options = null)
        : this(options, RunProcess)
    {
    }

    internal ClamWinScannerTestRuntime(
        ClamWinScannerTestRuntimeOptions? options,
        ClamWinScannerProcessRunner processRunner)
    {
        _options = options ?? new ClamWinScannerTestRuntimeOptions();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_options.Timeout.Ticks, 0);
        _processRunner = processRunner;
    }

    public ClamWinScannerTestResult TestConnection(
        string executablePath,
        string databasePath)
    {
        try
        {
            var cleanFile = WriteTestFile(CleanTestMessage);
            try
            {
                var cleanResult = ScanFile(executablePath, databasePath, cleanFile);
                if (cleanResult.VirusFound)
                {
                    return new ClamWinScannerTestResult(
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
                var eicarResult = ScanFile(executablePath, databasePath, eicarFile);
                return new ClamWinScannerTestResult(
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
            return new ClamWinScannerTestResult(false, ex.Message);
        }
    }

    internal static ProcessStartInfo CreateStartInfo(
        string executablePath,
        string databasePath,
        string filePath,
        string tempDirectory)
    {
        var fullFilePath = Path.GetFullPath(filePath);
        var workingDirectory = Path.GetDirectoryName(fullFilePath);
        if (string.IsNullOrEmpty(workingDirectory))
        {
            throw new ArgumentException("The scan file path must include a directory.", nameof(filePath));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = TrimMatchingQuotes(executablePath ?? string.Empty),
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--database=" + (databasePath ?? string.Empty));
        startInfo.ArgumentList.Add(Path.GetFileName(fullFilePath));
        startInfo.ArgumentList.Add("--tempdir=" + (tempDirectory ?? string.Empty));
        return startInfo;
    }

    private static ClamWinScannerProcessResult RunProcess(
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
                return new ClamWinScannerProcessResult(false, 0, LaunchFailureText);
            }

            if (!process.WaitForExit(timeout))
            {
                TryKill(process);
                return new ClamWinScannerProcessResult(false, 0, TimeoutText);
            }

            return new ClamWinScannerProcessResult(true, process.ExitCode);
        }
        catch (Exception ex) when (
            ex is Win32Exception
                or FileNotFoundException
                or InvalidOperationException
                or PlatformNotSupportedException)
        {
            return new ClamWinScannerProcessResult(false, 0, LaunchFailureText);
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

    private ClamWinScanResult ScanFile(
        string executablePath,
        string databasePath,
        string filePath)
    {
        var startInfo = CreateStartInfo(
            executablePath,
            databasePath,
            filePath,
            NormalizeDirectory(_options.TempDirectory));
        var processResult = _processRunner(startInfo, _options.Timeout);
        if (!processResult.Started)
        {
            return new ClamWinScanResult(
                false,
                string.IsNullOrEmpty(processResult.ResultText)
                    ? LaunchFailureText
                    : processResult.ResultText);
        }

        return processResult.ExitCode == 1
            ? new ClamWinScanResult(true, "Unknown")
            : new ClamWinScanResult(false, "Return code: " + processResult.ExitCode);
    }

    private string WriteTestFile(byte[] contents)
    {
        var dataDirectory = NormalizeDirectory(_options.DataDirectory);
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(NormalizeDirectory(_options.TempDirectory));
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

    private static string TrimMatchingQuotes(string value) =>
        value.Length >= 2
            && value[0] == '"'
            && value[^1] == '"'
                ? value[1..^1]
                : value;

    private sealed record ClamWinScanResult(
        bool VirusFound,
        string Details);
}
