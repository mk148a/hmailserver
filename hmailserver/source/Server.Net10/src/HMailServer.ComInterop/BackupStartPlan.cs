using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(false)]
internal sealed record BackupStartPlan(
    string Destination,
    int BackupOptions,
    bool BackupMessagesDbOnly,
    bool IncludesMessages,
    bool RequiresDataDirectoryCopy,
    string? FailureReason)
{
    internal const int BackupSettingsFlag = 1;
    internal const int BackupDomainsFlag = 2;
    internal const int BackupMessagesFlag = 4;
    internal const int BackupCompressionFlag = 8;

    internal bool CanStart => FailureReason is null;

    internal static BackupStartPlan Evaluate(
        string destination,
        int backupOptions,
        bool backupMessagesDbOnly,
        bool allMessageFilesInDataFolder,
        bool destinationExists)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var normalizedDestination = destination.Length > 0 && destination[^1] == '\\'
            ? destination[..^1]
            : destination;
        var includesMessages = (backupOptions & BackupMessagesFlag) != 0;
        var requiresDataDirectoryCopy =
            (backupOptions & (BackupDomainsFlag | BackupMessagesFlag)) ==
            (BackupDomainsFlag | BackupMessagesFlag) &&
            !backupMessagesDbOnly;

        if (includesMessages && !allMessageFilesInDataFolder)
        {
            return new BackupStartPlan(
                normalizedDestination,
                backupOptions,
                backupMessagesDbOnly,
                includesMessages,
                requiresDataDirectoryCopy,
                "All messages are not located in the data folder.");
        }

        if (!destinationExists)
        {
            return new BackupStartPlan(
                normalizedDestination,
                backupOptions,
                backupMessagesDbOnly,
                includesMessages,
                requiresDataDirectoryCopy,
                "The specified backup directory is not accessible: " + normalizedDestination);
        }

        return new BackupStartPlan(
            normalizedDestination,
            backupOptions,
            backupMessagesDbOnly,
            includesMessages,
            requiresDataDirectoryCopy,
            FailureReason: null);
    }
}
