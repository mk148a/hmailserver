namespace HMailServer.Storage.SqlServer;

public sealed record SqlServerImapMailboxStoreOptions
{
    public string HierarchyDelimiter { get; init; } = ".";

    public string PublicFolderName { get; init; } = "#Public";

    public bool UseAcl { get; init; } = true;
}
