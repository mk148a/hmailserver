using HMailServer.Core.Abstractions;
using Microsoft.Extensions.Configuration;

namespace HMailServer.Security;

public sealed class LegacyDirectoryAdministrationStore : IDirectoryAdministrationStore
{
    private readonly string _initializationFile;

    public LegacyDirectoryAdministrationStore(string initializationFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(initializationFile);
        _initializationFile = Path.GetFullPath(initializationFile);
    }

    public ValueTask<DirectoryAdministrationSnapshot> GetDirectoriesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var configuration = new ConfigurationBuilder()
            .AddIniFile(_initializationFile, optional: true, reloadOnChange: false)
            .Build();

        var programFolder = ReadDirectorySetting(configuration, "ProgramFolder");

        return ValueTask.FromResult(
            new DirectoryAdministrationSnapshot(
                ProgramDirectory: EnsureTrailingBackslash(programFolder),
                DatabaseDirectory: TrimSingleTrailingBackslash(ReadDirectorySetting(configuration, "DatabaseFolder")),
                DataDirectory: TrimSingleTrailingBackslash(ReadDirectorySetting(configuration, "DataFolder")),
                LogDirectory: ReadDirectorySetting(configuration, "LogFolder"),
                TempDirectory: TrimSingleTrailingBackslash(ReadDirectorySetting(configuration, "TempFolder")),
                EventDirectory: ReadDirectorySetting(configuration, "EventFolder"),
                DBScriptDirectory: EnsureTrailingBackslash(programFolder) + "DBScripts"));
    }

    public ValueTask<bool> UpdateLogDirectoryAsync(
        string logDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(logDirectory);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(LegacyInitializationFile.SaveLogDirectory(_initializationFile, logDirectory));
    }

    public ValueTask<bool> UpdateTempDirectoryAsync(
        string tempDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tempDirectory);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(LegacyInitializationFile.SaveTempDirectory(_initializationFile, tempDirectory));
    }

    public ValueTask<bool> UpdateDataDirectoryAsync(
        string dataDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataDirectory);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(LegacyInitializationFile.SaveDataDirectory(_initializationFile, dataDirectory));
    }

    private static string ReadDirectorySetting(IConfiguration configuration, string name) =>
        configuration[$"Directories:{name}"] ?? string.Empty;

    private static string EnsureTrailingBackslash(string value) =>
        value.EndsWith("\\", StringComparison.Ordinal) ? value : value + "\\";

    private static string TrimSingleTrailingBackslash(string value) =>
        value.EndsWith("\\", StringComparison.Ordinal) ? value[..^1] : value;
}
