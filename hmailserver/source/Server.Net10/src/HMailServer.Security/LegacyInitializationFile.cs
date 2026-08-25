using Microsoft.Extensions.Configuration;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace HMailServer.Security;

public static class LegacyInitializationFile
{
    public static string ResolvePath(string? configuredPath, string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(baseDirectory, "hMailServer.ini")
            : configuredPath;

        return Path.GetFullPath(path, baseDirectory);
    }

    public static string LoadAdministratorPasswordHash(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var configuration = new ConfigurationBuilder()
            .AddIniFile(Path.GetFullPath(path), optional: true, reloadOnChange: false)
            .Build();

        return configuration["Security:AdministratorPassword"]?.Trim() ?? string.Empty;
    }

    public static bool SaveAdministratorPasswordHash(string path, string hash) =>
        SaveAdministratorPasswordHash(path, hash, flushDirectory: null);

    internal static bool SaveAdministratorPasswordHash(
        string path,
        string hash,
        Action<string>? flushDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(hash);

        var targetPath = Path.GetFullPath(path);
        var temporaryPath = targetPath + $".{Guid.NewGuid():N}.ini";
        var targetExists = File.Exists(targetPath);

        try
        {
            if (targetExists)
            {
                File.Copy(targetPath, temporaryPath, overwrite: false);
            }
            else
            {
                using (File.Create(temporaryPath))
                {
                }
            }

            if (!WritePrivateProfileString(
                    "Security",
                    "AdministratorPassword",
                    hash,
                    temporaryPath))
            {
                return false;
            }

            _ = WritePrivateProfileString(null, null, null, temporaryPath);

            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read))
            {
                stream.Flush(flushToDisk: true);
            }

            if (targetExists)
            {
                File.Replace(temporaryPath, targetPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPath, targetPath);
            }

            var directoryPath = Path.GetDirectoryName(targetPath)
                ?? throw new IOException("The initialization file has no containing directory.");
            (flushDirectory ?? FlushDirectory)(directoryPath);

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
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

    private static void FlushDirectory(string directoryPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            using var directoryHandle = File.OpenHandle(
                directoryPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            RandomAccess.FlushToDisk(directoryHandle);
            return;
        }

        using var windowsDirectoryHandle = CreateFileW(
            directoryPath,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics,
            IntPtr.Zero);
        if (windowsDirectoryHandle.IsInvalid)
        {
            throw new IOException(
                "The initialization file directory could not be opened for durable finalization.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        if (!FlushFileBuffers(windowsDirectoryHandle))
        {
            throw new IOException(
                "The initialization file directory could not be flushed for durable finalization.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }
    }

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushFileBuffers(SafeFileHandle fileHandle);

    public static LegacyDatabaseConfiguration LoadDatabaseConfiguration(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var configuration = new ConfigurationBuilder()
            .AddIniFile(Path.GetFullPath(path), optional: true, reloadOnChange: false)
            .Build();

        var databaseType = ParseDatabaseType(configuration["Database:Type"]);

        return new LegacyDatabaseConfiguration(
            DatabaseType: databaseType,
            DatabaseExists: databaseType != 0,
            ServerName: configuration["Database:Server"] ?? string.Empty,
            DatabaseName: configuration["Database:Database"] ?? string.Empty);
    }

    public static string LoadUserInterfaceLanguage(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var configuration = new ConfigurationBuilder()
            .AddIniFile(Path.GetFullPath(path), optional: true, reloadOnChange: false)
            .Build();

        return configuration["Settings:UseLanguage"] ?? "English";
    }

    public static void SaveUserInterfaceLanguage(string path, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(value);

        _ = WritePrivateProfileString(
            "Settings",
            "UseLanguage",
            value,
            Path.GetFullPath(path));
    }

    public static IReadOnlyList<string> LoadValidGuiLanguages(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var configuration = new ConfigurationBuilder()
            .AddIniFile(Path.GetFullPath(path), optional: true, reloadOnChange: false)
            .Build();

        return SplitLegacyString(configuration["GUILanguages:ValidLanguages"] ?? string.Empty, ",");
    }

    public static bool LoadRewriteEnvelopeFromWhenForwarding(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var configuration = new ConfigurationBuilder()
            .AddIniFile(Path.GetFullPath(path), optional: true, reloadOnChange: false)
            .Build();

        return int.TryParse(
                configuration["Settings:RewriteEnvelopeFromWhenForwarding"],
                out var value)
                && value == 1;
    }

    public static void SaveRewriteEnvelopeFromWhenForwarding(string path, bool value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _ = WritePrivateProfileString(
            "Settings",
            "RewriteEnvelopeFromWhenForwarding",
            value ? "1" : "0",
            Path.GetFullPath(path));
    }

    public static bool SaveLogDirectory(string path, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(value);

        return WritePrivateProfileString(
            "Directories",
            "LogFolder",
            value,
            Path.GetFullPath(path));
    }

    public static bool LoadBackupMessagesDbOnly(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var configuration = new ConfigurationBuilder()
            .AddIniFile(Path.GetFullPath(path), optional: true, reloadOnChange: false)
            .Build();

        return int.TryParse(
                configuration["Settings:BackupMessagesDBOnly"],
                out var value)
            && value == 1;
    }

    private static int ParseDatabaseType(string? value)
    {
        return value switch
        {
            var text when string.Equals(text, "MYSQL", StringComparison.OrdinalIgnoreCase) => 1,
            var text when string.Equals(text, "MSSQL", StringComparison.OrdinalIgnoreCase) => 2,
            var text when string.Equals(text, "PostgreSQL", StringComparison.OrdinalIgnoreCase) => 3,
            var text when string.Equals(text, "MSSQLCE", StringComparison.OrdinalIgnoreCase) => 4,
            _ => 0
        };
    }

    [System.Runtime.InteropServices.DllImport(
        "kernel32.dll",
        CharSet = System.Runtime.InteropServices.CharSet.Unicode,
        EntryPoint = "WritePrivateProfileStringW",
        ExactSpelling = true,
        SetLastError = true)]
    private static extern bool WritePrivateProfileString(
        string? section,
        string? key,
        string? value,
        string filePath);

    private static IReadOnlyList<string> SplitLegacyString(string value, string separator)
    {
        if (value.Length == 0)
        {
            return [];
        }

        var result = new List<string>();
        var beginning = 0;
        var end = value.IndexOf(separator, StringComparison.Ordinal);

        if (end == -1)
        {
            result.Add(value);
            return result;
        }

        while (end >= 0)
        {
            result.Add(value[beginning..end]);
            beginning = end + separator.Length;
            end = value.IndexOf(separator, beginning, StringComparison.Ordinal);
        }

        if (beginning > 0)
        {
            var remainder = value[beginning..];
            if (remainder.Length > 0)
            {
                result.Add(remainder);
            }
        }

        return result;
    }
}

public sealed record LegacyDatabaseConfiguration(
    int DatabaseType,
    bool DatabaseExists,
    string ServerName,
    string DatabaseName);
