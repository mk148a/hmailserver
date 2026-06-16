namespace HMailServer.Storage.SqlServer;

public sealed record SqlServerImapMailboxPath(
    bool IsPublicFolder,
    IReadOnlyList<string> Segments)
{
    public static SqlServerImapMailboxPath? Parse(
        string mailboxName,
        string hierarchyDelimiter,
        string publicFolderName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mailboxName);
        ArgumentException.ThrowIfNullOrWhiteSpace(hierarchyDelimiter);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicFolderName);

        var segments = mailboxName
            .Split([hierarchyDelimiter], StringSplitOptions.None)
            .Select(static segment => segment.Trim())
            .Where(static segment => segment.Length > 0)
            .ToArray();

        if (segments.Length == 0)
        {
            return null;
        }

        var isPublic = segments[0].Equals(publicFolderName, StringComparison.OrdinalIgnoreCase);
        if (!isPublic)
        {
            return new SqlServerImapMailboxPath(false, segments);
        }

        var publicSegments = segments.Skip(1).ToArray();
        return publicSegments.Length == 0
            ? null
            : new SqlServerImapMailboxPath(true, publicSegments);
    }
}
