namespace HMailServer.Storage.SqlServer;

public sealed record MessageFileSearchDocumentSourceOptions(
    string DataDirectory,
    int MaxHeaderChars = 128 * 1024,
    int MaxBodyChars = 1024 * 1024,
    int MaxCombinedChars = 1024 * 1024,
    string PublicFolderDiskName = "#Public")
{
    public string NormalizedDataDirectory
    {
        get
        {
            var fullPath = Path.GetFullPath(DataDirectory);
            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
