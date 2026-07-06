using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

public sealed class StoreBackedMessageIdResolver : IMessageIdResolver
{
    private const string PublicFolderDiskName = "#Public";
    private const char PathSeparator = '\\';

    private readonly IMessageFileNameLookup _lookup;
    private readonly string _dataDirectory;

    public StoreBackedMessageIdResolver(
        IMessageFileNameLookup lookup,
        string dataDirectory)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _lookup = lookup;
        _dataDirectory = TrimSingleTrailingBackslash(dataDirectory);
    }

    public async ValueTask<long> RetrieveMessageIdAsync(
        string fileName,
        CancellationToken cancellationToken)
    {
        fileName ??= string.Empty;
        if (TryGetPartialFileName(fileName, _dataDirectory, out var partialFileName))
        {
            var partialMatch = await _lookup
                .GetMessageIdByFileNameAsync(partialFileName, cancellationToken)
                .ConfigureAwait(false);
            if (partialMatch.HasValue)
            {
                return partialMatch.Value;
            }
        }

        return await _lookup
                .GetMessageIdByFileNameAsync(fileName, cancellationToken)
                .ConfigureAwait(false)
            ?? 0;
    }

    internal static bool TryGetPartialFileName(
        string fullPath,
        string dataDirectory,
        out string partialFileName)
    {
        partialFileName = string.Empty;
        if (!fullPath.StartsWith(dataDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var filePath = LegacySubstring(fullPath, dataDirectory.Length + 1);
        if (filePath.StartsWith(PublicFolderDiskName, StringComparison.OrdinalIgnoreCase))
        {
            filePath = LegacySubstring(filePath, PublicFolderDiskName.Length + 1);
            var guidSlashPosition = filePath.IndexOf(PathSeparator);
            if (guidSlashPosition <= 0 || guidSlashPosition != 2)
            {
                return false;
            }

            var lastLevelName = LegacySubstring(filePath, guidSlashPosition);
            filePath = LegacySubstring(filePath, guidSlashPosition + 1);
            if (!string.Equals(
                    lastLevelName,
                    LegacySubstring(filePath, 1, 2),
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        else
        {
            var domainSlashPosition = filePath.IndexOf(PathSeparator);
            if (domainSlashPosition >= 0)
            {
                var accountSlashPosition = filePath.IndexOf(
                    PathSeparator,
                    domainSlashPosition + 1);
                if (accountSlashPosition <= 0)
                {
                    return false;
                }

                var guidSlashPosition = filePath.IndexOf(
                    PathSeparator,
                    accountSlashPosition + 1);
                if (guidSlashPosition <= 0)
                {
                    return false;
                }

                var lastLevelLength = guidSlashPosition - accountSlashPosition - 1;
                if (lastLevelLength != 2)
                {
                    return false;
                }

                var lastLevelName = LegacySubstring(
                    filePath,
                    accountSlashPosition + 1,
                    lastLevelLength);
                filePath = LegacySubstring(filePath, guidSlashPosition + 1);
                if (!string.Equals(
                        lastLevelName,
                        LegacySubstring(filePath, 1, 2),
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        partialFileName = filePath;
        return true;
    }

    private static string TrimSingleTrailingBackslash(string value) =>
        value.EndsWith(PathSeparator) ? value[..^1] : value;

    private static string LegacySubstring(string value, int start) =>
        start > value.Length ? string.Empty : value[start..];

    private static string LegacySubstring(string value, int start, int length)
    {
        if (start > value.Length)
        {
            return string.Empty;
        }

        return value.Substring(start, Math.Min(length, value.Length - start));
    }
}
