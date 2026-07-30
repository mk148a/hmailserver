using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupStartPlanRuntimeTests
{
    [TestMethod]
    public async Task GetEvidenceLoadsConfiguredSettingsAndOnlyQueriesMessagesWhenSelected()
    {
        var settingsStore = new FixedSettingsStore(
            new SettingsAdministrationSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                BackupDestination: @"D:\MailBackup\",
                BackupOptions: 2));
        var preflightStore = new RecordingBackupPreflightStore(allMessageFilesInDataDirectory: false);
        var runtime = new BackupStartPlanRuntime(
            settingsStore,
            preflightStore,
            dataDirectory: @"C:\hMailServer\Data",
            backupMessagesDbOnly: false,
            pathExists: _ => false);

        var evidence = await runtime.GetEvidenceAsync(CancellationToken.None);

        Assert.AreEqual(@"D:\MailBackup\", evidence.Destination);
        Assert.AreEqual(2, evidence.BackupOptions);
        Assert.IsTrue(evidence.AllMessageFilesInDataDirectory);
        Assert.IsFalse(evidence.DestinationExists);
        Assert.IsFalse(preflightStore.WasCalled);
    }

    [TestMethod]
    public async Task GetEvidenceReadsMessagePlacementAndNormalizesDestinationBeforeProbe()
    {
        var settingsStore = new FixedSettingsStore(
            new SettingsAdministrationSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                BackupDestination: @"D:\MailBackup\",
                BackupOptions: 4));
        var preflightStore = new RecordingBackupPreflightStore(allMessageFilesInDataDirectory: true);
        string? probedPath = null;
        var runtime = new BackupStartPlanRuntime(
            settingsStore,
            preflightStore,
            dataDirectory: @"C:\hMailServer\Data",
            backupMessagesDbOnly: true,
            pathExists: path =>
            {
                probedPath = path;
                return true;
            });

        var evidence = await runtime.GetEvidenceAsync(CancellationToken.None);

        Assert.IsTrue(evidence.AllMessageFilesInDataDirectory);
        Assert.IsTrue(evidence.DestinationExists);
        Assert.AreEqual(@"D:\MailBackup", probedPath);
        Assert.AreEqual(@"C:\hMailServer\Data", preflightStore.DataDirectory);
        Assert.IsTrue(preflightStore.WasCalled);
    }

    [TestMethod]
    public async Task QueuedTaskFailsThroughLegacyPreflightCallbackBeforeExecution()
    {
        using var queue = new BackupTaskQueue();
        var executed = false;
        var runtime = new BackupOperationRuntime(
            queue,
            _ => new ValueTask<BackupStartPlanEvidence>(
                new BackupStartPlanEvidence(
                    Destination: @"D:\MailBackup",
                    BackupOptions: 4,
                    BackupMessagesDbOnly: true,
                    AllMessageFilesInDataDirectory: false,
                    DestinationExists: true)),
            (_, _) =>
            {
                executed = true;
                return ValueTask.CompletedTask;
            });
        var manager = BackupManager.CreateAuthorized(
            new RecordingBackupArchiveMetadataReader(0),
            runtime);

        manager.StartBackup();

        await using var reader = queue.ReadAllAsync(CancellationToken.None).GetAsyncEnumerator();
        Assert.IsTrue(await reader.MoveNextAsync());
        var task = reader.Current;

        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => task.ExecuteAsync(CancellationToken.None).AsTask());

        Assert.AreEqual("All messages are not located in the data folder.", error.Message);
        Assert.IsFalse(executed);
    }

    [TestMethod]
    public async Task QueuedTaskPassesSuccessfulPlanEvidenceToArchiveRuntime()
    {
        using var queue = new BackupTaskQueue();
        BackupStartPlanEvidence? observed = null;
        var expected = new BackupStartPlanEvidence(
            Destination: @"D:\MailBackup",
            BackupOptions: 8,
            BackupMessagesDbOnly: false,
            AllMessageFilesInDataDirectory: true,
            DestinationExists: true);
        var runtime = new BackupOperationRuntime(
            queue,
            _ => new ValueTask<BackupStartPlanEvidence>(expected),
            (evidence, _) =>
            {
                observed = evidence;
                return ValueTask.CompletedTask;
            });
        var manager = BackupManager.CreateAuthorized(
            new RecordingBackupArchiveMetadataReader(0),
            runtime);

        manager.StartBackup();

        await using var reader = queue.ReadAllAsync(CancellationToken.None).GetAsyncEnumerator();
        Assert.IsTrue(await reader.MoveNextAsync());

        await reader.Current.ExecuteAsync(CancellationToken.None);

        Assert.AreSame(expected, observed);
    }

    private sealed class FixedSettingsStore(SettingsAdministrationSnapshot snapshot)
        : ISettingsAdministrationStore
    {
        public ValueTask<SettingsAdministrationSnapshot> GetSettingsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(snapshot);
    }

    private sealed class RecordingBackupPreflightStore(bool allMessageFilesInDataDirectory)
        : IBackupPreflightAdministrationStore
    {
        public string? DataDirectory { get; private set; }

        public bool WasCalled { get; private set; }

        public ValueTask<bool> AreAllMessageFilesInDataDirectoryAsync(
            string dataDirectory,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            DataDirectory = dataDirectory;
            return ValueTask.FromResult(allMessageFilesInDataDirectory);
        }
    }

    private sealed class RecordingBackupArchiveMetadataReader(int options)
        : IBackupArchiveMetadataReader
    {
        public int ReadContainsOptions(string archivePath) => options;
    }
}
