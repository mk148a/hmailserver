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
}

public sealed record LegacyDatabaseConfiguration(
    int DatabaseType,
    bool DatabaseExists,
    string ServerName,
    string DatabaseName);
