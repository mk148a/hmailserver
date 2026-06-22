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
}
