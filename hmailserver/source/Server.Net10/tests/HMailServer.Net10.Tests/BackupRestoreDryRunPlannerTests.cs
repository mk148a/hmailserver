using HMailServer.ComInterop;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupRestoreDryRunPlannerTests
{
    [TestMethod]
    public void Plan_ReportsLegacySelectionsAndOrdering()
    {
        var evidence = CreateEvidence(
            mode: 15,
            dataFilesFormat: "7z",
            rawDataBackupPath: null);

        var plan = BackupRestoreDryRunPlanner.Plan(evidence, requestedRestoreOptions: 7);

        Assert.AreEqual(evidence.ArchivePath, plan.ArchivePath);
        Assert.IsTrue(plan.EvidenceIsValid);
        Assert.AreEqual(15, plan.Mode);
        Assert.IsTrue(plan.ContainsSettings);
        Assert.IsTrue(plan.ContainsDomains);
        Assert.IsTrue(plan.ContainsMessages);
        Assert.IsTrue(plan.RestoreSettings);
        Assert.IsTrue(plan.RestoreDomains);
        Assert.IsTrue(plan.RestoreMessages);
        CollectionAssert.AreEqual(
            new[]
            {
                BackupRestoreDryRunPlanner.DropDomainsStep,
                BackupRestoreDryRunPlanner.DropPublicFoldersStep,
                BackupRestoreDryRunPlanner.RestoreDataDirectoryStep,
                BackupRestoreDryRunPlanner.LoadDomainsAndChildrenStep,
                BackupRestoreDryRunPlanner.LoadSettingsStep,
                BackupRestoreDryRunPlanner.ReinitializeStep
            },
            plan.Steps.ToArray());
        Assert.IsNull(plan.FailureReason);
        Assert.IsFalse(plan.WouldMutate);
    }

    [TestMethod]
    public void Plan_RestoreMessagesOnlyWarnsAndDoesNotPlanRestoreWork()
    {
        var plan = BackupRestoreDryRunPlanner.Plan(
            CreateEvidence(mode: 4, dataFilesFormat: "7z", rawDataBackupPath: null),
            requestedRestoreOptions: 4);

        CollectionAssert.AreEqual(
            new[] { BackupRestoreDryRunPlanner.ReinitializeStep },
            plan.Steps.ToArray());
        StringAssert.Contains(plan.Warnings.Single(), "RestoreMessages");
        StringAssert.Contains(plan.Warnings.Single(), "RestoreDomains");
        Assert.IsFalse(plan.WouldMutate);
    }

    [TestMethod]
    public void Plan_SettingsOnlyLoadsSettingsBeforeReinitialize()
    {
        var plan = BackupRestoreDryRunPlanner.Plan(
            CreateEvidence(mode: 1, dataFilesFormat: null, rawDataBackupPath: null),
            requestedRestoreOptions: 1);

        CollectionAssert.AreEqual(
            new[]
            {
                BackupRestoreDryRunPlanner.LoadSettingsStep,
                BackupRestoreDryRunPlanner.ReinitializeStep
            },
            plan.Steps.ToArray());
    }

    [TestMethod]
    public void Plan_DbOnlyOmitsPhysicalCleanupAndDataRestore()
    {
        var evidence = CreateEvidence(
            mode: 15,
            dataFilesFormat: "7z",
            rawDataBackupPath: null,
            backupMessagesDbOnly: true);

        var plan = BackupRestoreDryRunPlanner.Plan(evidence, requestedRestoreOptions: 7);

        Assert.IsTrue(plan.BackupMessagesDbOnly);
        CollectionAssert.AreEqual(
            new[]
            {
                BackupRestoreDryRunPlanner.LoadDomainsAndChildrenStep,
                BackupRestoreDryRunPlanner.LoadSettingsStep,
                BackupRestoreDryRunPlanner.ReinitializeStep
            },
            plan.Steps.ToArray());
        Assert.IsFalse(plan.Steps.Contains(BackupRestoreDryRunPlanner.DropDomainsStep));
        Assert.IsFalse(plan.Steps.Contains(BackupRestoreDryRunPlanner.DropPublicFoldersStep));
        Assert.IsFalse(plan.Steps.Contains(BackupRestoreDryRunPlanner.RestoreDataDirectoryStep));
    }

    [TestMethod]
    public void Plan_PreservesCompressedAndRawDataEvidence()
    {
        var compressed = BackupRestoreDryRunPlanner.Plan(
            CreateEvidence(mode: 12, dataFilesFormat: "7z", rawDataBackupPath: null),
            requestedRestoreOptions: 6);
        var rawPath = Path.Combine(Path.GetTempPath(), "DataBackup");
        var raw = BackupRestoreDryRunPlanner.Plan(
            CreateEvidence(mode: 6, dataFilesFormat: "Raw", rawDataBackupPath: rawPath),
            requestedRestoreOptions: 6);

        Assert.AreEqual("7z", compressed.DataFilesFormat);
        Assert.IsNull(compressed.RawDataBackupPath);
        Assert.AreEqual("Raw", raw.DataFilesFormat);
        Assert.AreEqual(rawPath, raw.RawDataBackupPath);
    }

    [TestMethod]
    public void Plan_IsPureAndFailsWithoutMutationForInvalidEvidence()
    {
        var evidence = CreateEvidence(
            mode: 15,
            dataFilesFormat: "7z",
            rawDataBackupPath: null) with
        {
            IsValid = false,
            FailureReason = "invalid evidence"
        };

        var plan = BackupRestoreDryRunPlanner.Plan(evidence, requestedRestoreOptions: 7);

        Assert.AreEqual("invalid evidence", plan.FailureReason);
        Assert.IsFalse(plan.WouldMutate);
        Assert.IsEmpty(plan.Steps);
        Assert.AreEqual(evidence, plan.Evidence);
    }

    private static BackupRestoreIntegrityEvidence CreateEvidence(
        int mode,
        string? dataFilesFormat,
        string? rawDataBackupPath,
        bool backupMessagesDbOnly = false) =>
        new(@"C:\backups\backup.7z")
        {
            IsValid = true,
            ArchiveTestPassed = true,
            MetadataPresent = true,
            MetadataXmlValid = true,
            BackupOptions = mode,
            BackupMessagesDbOnly = backupMessagesDbOnly,
            DataFilesFormat = dataFilesFormat,
            RawDataBackupPath = rawDataBackupPath
        };
}
