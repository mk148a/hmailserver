using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Service;
using Microsoft.Extensions.Logging.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class BackupManagerMode7DispatchTests
{
    [TestMethod]
    public async Task Mode7_StartBackupLoadBackupSelectAllAndStartRestore_DispatchesThroughQueue()
    {
        var sevenZipPath = Path.Combine(AppContext.BaseDirectory, "7za.exe");
        if (!File.Exists(sevenZipPath))
        {
            Assert.Inconclusive($"The isolated dispatch smoke test requires {sevenZipPath}.");
        }

        var destination = Path.Combine(
            Path.GetTempPath(),
            $"hmailserver-backup-mode7-{Guid.NewGuid():N}");
        Directory.CreateDirectory(destination);
        BackupTaskHostedService? service = null;
        Backup? backup = null;

        try
        {
            var dataDirectory = Path.Combine(destination, "data");
            Directory.CreateDirectory(Path.Combine(dataDirectory, "example.test", "user", "ne"));
            File.WriteAllText(Path.Combine(dataDirectory, "root.txt"), "omit me");
            File.WriteAllText(
                Path.Combine(dataDirectory, "example.test", "user", "ne", "one.eml"),
                "From: sender@example.test\r\n\r\nbody");

            using var queue = new RecordingBackupTaskQueue();
            var readiness = new ServerReadinessSignal();
            readiness.SetBootstrapComplete();
            service = new BackupTaskHostedService(
                queue,
                NullLogger<BackupTaskHostedService>.Instance,
                readiness);
            var backupCompleted = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var backupThreadStopped = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var restoreExecutor = new RecordingRestoreExecutor();
            var evidence = new BackupStartPlanEvidence(
                Destination: destination,
                BackupOptions: 1 | 2 | 4,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true);
            var archiveRuntime = new SevenZipBackupArchiveRuntime(
                sevenZipPath,
                "10.0.0-B0",
                static () => new DateTime(2026, 8, 11, 4, 5, 6),
                payloadProvider: static (_, _) => ValueTask.FromResult(
                    new BackupArchiveXmlPayload(
                        Settings: new SettingsAdministrationSnapshot(
                            "mail.example.test",
                            "smtp",
                            "pop3",
                            "imap"),
                        Domains: new[]
                        {
                            new DomainAdministrationSnapshot(10, "example.test", true)
                        })),
                dataDirectory: dataDirectory);
            var innerOperationRuntime = new BackupOperationRuntime(
                queue,
                startPlanEvidence: _ => ValueTask.FromResult(evidence),
                executeBackupAsync: async (startEvidence, cancellationToken) =>
                {
                    try
                    {
                        await archiveRuntime.CreateAsync(startEvidence, cancellationToken);
                        backupCompleted.TrySetResult(
                            Path.Combine(destination, "HMBackup 2026-08-11 040506.7z"));
                    }
                    catch (Exception exception)
                    {
                        backupCompleted.TrySetException(exception);
                        throw;
                    }
                });
            var operationRuntime = new CompletionObservingOperationRuntime(
                innerOperationRuntime,
                backupThreadStopped);
            var manager = BackupManager.CreateAuthorized(
                new SevenZipBackupArchiveMetadataReader(sevenZipPath),
                operationRuntime,
                restoreExecutor: restoreExecutor);

            await service.StartAsync(CancellationToken.None);

            manager.StartBackup();
            Assert.AreEqual(1, queue.EnqueueCount);

            var archivePath = await backupCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsTrue(File.Exists(archivePath), archivePath);
            var dataBackup = Path.Combine(destination, "DataBackup");
            Assert.IsTrue(Directory.Exists(dataBackup), dataBackup);
            Assert.IsFalse(File.Exists(Path.Combine(dataBackup, "root.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(dataBackup, "example.test", "user", "ne", "one.eml")));

            await backupThreadStopped.Task.WaitAsync(TimeSpan.FromSeconds(10));

            backup = (Backup)manager.LoadBackup(archivePath);
            Assert.IsTrue(backup.ContainsSettings);
            Assert.IsTrue(backup.ContainsDomains);
            Assert.IsTrue(backup.ContainsMessages);

            backup.RestoreSettings = true;
            backup.RestoreDomains = true;
            backup.RestoreMessages = true;
            backup.StartRestore();
            Assert.AreEqual(2, queue.EnqueueCount);

            var restore = await restoreExecutor.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreEqual(7, restore.RestoreOptions);
            Assert.IsTrue(restore.ArchiveExistsAtExecution);
            StringAssert.EndsWith(restore.ArchivePath, "archive.7z");
        }
        finally
        {
            backup?.CleanupArchiveBinding();
            try
            {
                if (service is not null)
                {
                    await service.StopAsync(CancellationToken.None);
                }
            }
            finally
            {
                if (Directory.Exists(destination))
                {
                    Directory.Delete(destination, recursive: true);
                }
            }
        }
    }

    private sealed class CompletionObservingOperationRuntime : IBackupOperationRuntime
    {
        private readonly IBackupOperationRuntime _inner;
        private readonly TaskCompletionSource<object?> _firstTaskStopped;

        internal CompletionObservingOperationRuntime(
            IBackupOperationRuntime inner,
            TaskCompletionSource<object?> firstTaskStopped)
        {
            _inner = inner;
            _firstTaskStopped = firstTaskStopped;
        }

        public BackupStartDispatchResult TryStartBackup(Func<BackupTaskRequest> taskFactory) =>
            _inner.TryStartBackup(taskFactory);

        public BackupStartDispatchResult TryStartRestore(Func<BackupTaskRequest> taskFactory) =>
            _inner.TryStartRestore(taskFactory);

        public void OnThreadStopped()
        {
            _inner.OnThreadStopped();
            _firstTaskStopped.TrySetResult(null);
        }
    }

    private sealed class RecordingRestoreExecutor : IBackupRestoreExecutor
    {
        internal TaskCompletionSource<RestoreEvidence> Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask ExecuteAsync(Backup backup, CancellationToken cancellationToken)
        {
            var archivePath = backup.ArchivePath;
            Completed.TrySetResult(
                new RestoreEvidence(
                    archivePath,
                    backup.RestoreOptions,
                    File.Exists(archivePath)));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingBackupTaskQueue : IBackupTaskQueue, IDisposable
    {
        private readonly BackupTaskQueue _inner = new();

        internal int EnqueueCount { get; private set; }

        public bool TryEnqueue(BackupTaskRequest request)
        {
            var accepted = _inner.TryEnqueue(request);
            if (accepted)
            {
                EnqueueCount++;
            }

            return accepted;
        }

        public IAsyncEnumerable<BackupTaskRequest> ReadAllAsync(CancellationToken cancellationToken) =>
            _inner.ReadAllAsync(cancellationToken);

        public void StopAccepting() => _inner.StopAccepting();

        public void CompleteAndAbortPending() => _inner.CompleteAndAbortPending();

        public void Dispose() => _inner.Dispose();
    }

    private sealed record RestoreEvidence(
        string ArchivePath,
        int RestoreOptions,
        bool ArchiveExistsAtExecution);
}
