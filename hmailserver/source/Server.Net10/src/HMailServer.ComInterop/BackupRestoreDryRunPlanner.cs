using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(false)]
internal static class BackupRestoreDryRunPlanner
{
    internal const string DropDomainsStep = "Drop domains";
    internal const string DropPublicFoldersStep = "Drop public folders";
    internal const string RestoreDataDirectoryStep = "Restore data directory";
    internal const string LoadDomainsAndChildrenStep = "Load domains/children";
    internal const string LoadSettingsStep = "Load settings";
    internal const string ReinitializeStep = "Reinitialize";

    internal static BackupRestoreDryRunPlan Plan(
        BackupRestoreIntegrityEvidence evidence,
        int requestedRestoreOptions)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var mode = evidence.BackupOptions;
        var containsSettings = HasFlag(mode, BackupStartPlan.BackupSettingsFlag);
        var containsDomains = HasFlag(mode, BackupStartPlan.BackupDomainsFlag);
        var containsMessages = HasFlag(mode, BackupStartPlan.BackupMessagesFlag);
        var restoreSettings = HasFlag(requestedRestoreOptions, BackupStartPlan.BackupSettingsFlag);
        var restoreDomains = HasFlag(requestedRestoreOptions, BackupStartPlan.BackupDomainsFlag);
        var restoreMessages = HasFlag(requestedRestoreOptions, BackupStartPlan.BackupMessagesFlag);
        var steps = ImmutableArray.CreateBuilder<string>();
        var warnings = ImmutableArray.CreateBuilder<string>();
        string? failureReason = null;

        if (!evidence.IsValid)
        {
            failureReason = evidence.FailureReason ?? "The backup restore evidence is invalid.";
        }
        else
        {
            if (restoreMessages && !restoreDomains)
            {
                warnings.Add(
                    "RestoreMessages has no legacy restore effect unless RestoreDomains is also selected.");
            }

            if (restoreDomains)
            {
                if (!evidence.BackupMessagesDbOnly)
                {
                    steps.Add(DropDomainsStep);
                }

                if (restoreSettings && !evidence.BackupMessagesDbOnly)
                {
                    steps.Add(DropPublicFoldersStep);
                }

                if (restoreMessages && !evidence.BackupMessagesDbOnly)
                {
                    steps.Add(RestoreDataDirectoryStep);
                }

                steps.Add(LoadDomainsAndChildrenStep);
            }

            if (restoreSettings)
            {
                steps.Add(LoadSettingsStep);
            }

            // Legacy reinitialization follows the restore-selection branches and is always requested
            // after valid restore metadata, including a RestoreMessages-only selection.
            steps.Add(ReinitializeStep);
        }

        return new BackupRestoreDryRunPlan(
            Evidence: evidence,
            RequestedRestoreOptions: requestedRestoreOptions,
            RestoreSettings: restoreSettings,
            RestoreDomains: restoreDomains,
            RestoreMessages: restoreMessages,
            Steps: steps.ToImmutable(),
            Warnings: warnings.ToImmutable(),
            FailureReason: failureReason,
            Mode: mode,
            ContainsSettings: containsSettings,
            ContainsDomains: containsDomains,
            ContainsMessages: containsMessages,
            DataFilesFormat: evidence.DataFilesFormat,
            RawDataBackupPath: evidence.RawDataBackupPath,
            BackupMessagesDbOnly: evidence.BackupMessagesDbOnly);
    }

    private static bool HasFlag(int? value, int flag) =>
        value.HasValue && HasFlag(value.Value, flag);

    private static bool HasFlag(int value, int flag) => (value & flag) != 0;
}

[ComVisible(false)]
internal sealed record BackupRestoreDryRunPlan(
    BackupRestoreIntegrityEvidence Evidence,
    int RequestedRestoreOptions,
    bool RestoreSettings,
    bool RestoreDomains,
    bool RestoreMessages,
    ImmutableArray<string> Steps,
    ImmutableArray<string> Warnings,
    string? FailureReason,
    int? Mode,
    bool ContainsSettings,
    bool ContainsDomains,
    bool ContainsMessages,
    string? DataFilesFormat,
    string? RawDataBackupPath,
    bool BackupMessagesDbOnly)
{
    internal bool WouldMutate => false;
    internal string ArchivePath => Evidence.ArchivePath;
    internal bool EvidenceIsValid => Evidence.IsValid;
    internal bool ArchiveTestPassed => Evidence.ArchiveTestPassed;
    internal bool MetadataPresent => Evidence.MetadataPresent;
    internal bool MetadataXmlValid => Evidence.MetadataXmlValid;
}
