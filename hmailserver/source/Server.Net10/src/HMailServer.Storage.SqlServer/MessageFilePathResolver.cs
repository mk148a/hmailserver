namespace HMailServer.Storage.SqlServer;

public sealed class MessageFilePathResolver
{
    private readonly MessageFileSearchDocumentSourceOptions _options;
    private readonly string _dataDirectory;

    public MessageFilePathResolver(MessageFileSearchDocumentSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DataDirectory);

        _options = options;
        _dataDirectory = options.NormalizedDataDirectory;
    }

    public string? Resolve(
        string messageFileName,
        int accountId,
        int folderId,
        string? accountAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageFileName);

        var candidate = Path.IsPathRooted(messageFileName)
            ? messageFileName
            : ResolveRelative(messageFileName, accountId, folderId, accountAddress);

        var fullPath = Path.GetFullPath(candidate);
        return IsUnderDataDirectory(fullPath) ? fullPath : null;
    }

    private string ResolveRelative(
        string messageFileName,
        int accountId,
        int folderId,
        string? accountAddress)
    {
        if (accountId > 0 && !string.IsNullOrWhiteSpace(accountAddress))
        {
            var at = accountAddress.LastIndexOf('@');
            if (at > 0 && at < accountAddress.Length - 1)
            {
                var localPart = accountAddress[..at];
                var domainPart = accountAddress[(at + 1)..];
                return Path.Combine(
                    _dataDirectory,
                    domainPart,
                    localPart,
                    GetGuidBucket(messageFileName),
                    messageFileName);
            }
        }

        if (folderId > 0)
        {
            return Path.Combine(
                _dataDirectory,
                _options.PublicFolderDiskName,
                GetGuidBucket(messageFileName),
                messageFileName);
        }

        return Path.Combine(_dataDirectory, messageFileName);
    }

    private bool IsUnderDataDirectory(string fullPath)
    {
        return fullPath.Equals(_dataDirectory, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(_dataDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(_dataDirectory + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetGuidBucket(string messageFileName)
    {
        var fileName = Path.GetFileName(messageFileName);
        return fileName.Length >= 2 ? fileName[..2] : fileName;
    }
}
