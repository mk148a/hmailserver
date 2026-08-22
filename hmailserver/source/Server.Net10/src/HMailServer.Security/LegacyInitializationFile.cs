using Microsoft.Extensions.Configuration;

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

    public static bool SaveAdministratorPasswordHash(string path, string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(hash);

        return WritePrivateProfileString(
            "Security",
            "AdministratorPassword",
            hash,
            Path.GetFullPath(path));
    }

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
