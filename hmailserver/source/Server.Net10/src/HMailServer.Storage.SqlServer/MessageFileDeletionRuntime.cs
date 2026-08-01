using HMailServer.Core.Abstractions;

namespace HMailServer.Storage.SqlServer;

public sealed class MessageFileDeletionRuntime
{
    public const int MaxAttempts = 5;

    private readonly MessageFilePathResolver _pathResolver;
    private readonly Func<string, bool> _deleteFile;
    private readonly TimeSpan _retryDelay;

    public MessageFileDeletionRuntime(
        MessageFilePathResolver pathResolver,
        Func<string, bool>? deleteFile = null,
        TimeSpan? retryDelay = null)
    {
        ArgumentNullException.ThrowIfNull(pathResolver);

        _pathResolver = pathResolver;
        _deleteFile = deleteFile ?? DeletePhysicalFile;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
        if (_retryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay));
        }
    }

    public bool TryDeleteAll(ImapFolderAdministrationDeletionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Succeeded)
        {
            return false;
        }

        var succeeded = true;
        foreach (var message in result.DeletedMessages)
        {
            if (!TryDelete(message))
            {
                succeeded = false;
            }
        }

        return succeeded;
    }

    public bool TryDelete(ImapFolderAdministrationDeletedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.FileName.Length == 0)
        {
            return true;
        }

        if (message.FileName.IndexOfAny(['\\', '/', ':']) >= 0
            || message.MessageType is < 0 or > 2)
        {
            return false;
        }

        string? path;
        try
        {
            path = _pathResolver.Resolve(
                message.FileName,
                message.AccountId,
                message.FolderId,
                message.AccountAddress);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }

        if (path is null || ContainsReparsePoint(path))
        {
            return false;
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            if (_deleteFile(path))
            {
                return true;
            }

            if (attempt < MaxAttempts && _retryDelay > TimeSpan.Zero)
            {
                Thread.Sleep(_retryDelay);
            }
        }

        return false;
    }

    private bool ContainsReparsePoint(string fullPath)
    {
        try
        {
            if (Directory.Exists(_pathResolver.DataDirectory)
                && (File.GetAttributes(_pathResolver.DataDirectory) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }

        var relativePath = Path.GetRelativePath(_pathResolver.DataDirectory, fullPath);
        if (relativePath == "." || relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            return true;
        }

        var currentPath = _pathResolver.DataDirectory;
        var parts = relativePath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            currentPath = Path.Combine(currentPath, part);
            if (!File.Exists(currentPath) && !Directory.Exists(currentPath))
            {
                break;
            }

            try
            {
                if ((File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        return false;
    }

    private static bool DeletePhysicalFile(string path)
    {
        try
        {
            File.Delete(path);
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
    }
}
