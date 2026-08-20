using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Service;
using Microsoft.Extensions.Logging.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class BackupFailureCleanupIntegrationTests
{
    [TestMethod]
    public async Task QueuedAuthenticatedStartBackup_DispatchesFailureAfterArchiveCleanup()
    {
        using var filesystem = new TemporaryFilesystem();
        var destination = Path.Combine(filesystem.RootPath, "backup");
        var dataDirectory = Path.Combine(filesystem.RootPath, "data");
        Directory.CreateDirectory(destination);
        Directory.CreateDirectory(Path.Combine(dataDirectory, "example.test", "user"));
        File.WriteAllText(
            Path.Combine(dataDirectory, "example.test", "user", "message.eml"),
            "From: sender@example.test\r\n\r\nbody");

        var metadataPath = Path.Combine(destination, "hMailServerBackup.xml");
        var dataBackupPath = Path.Combine(destination, "DataBackup");
        var failingExecutable = Path.Combine(filesystem.RootPath, "missing-7za.exe");
        using var queue = new BackupTaskQueue();
        BackupTaskHostedService? service = null;

        try
        {
            var dispatcher = new RecordingBackupEventDispatcher(metadataPath, dataBackupPath);
            var archiveRuntime = new SevenZipBackupArchiveRuntime(
                failingExecutable,
                "10.0.0-B0",
                static () => new DateTime(2026, 8, 20, 4, 5, 6),
                payloadProvider: static (_, _) => ValueTask.FromResult(
                    new BackupArchiveXmlPayload(
                        Settings: null,
                        Domains: new[]
                        {
                            new DomainAdministrationSnapshot(10, "example.test", true)
                        })),
                dataDirectory: dataDirectory);
            var evidence = new BackupStartPlanEvidence(
                Destination: destination,
                BackupOptions: 2 | 4,
                BackupMessagesDbOnly: false,
                AllMessageFilesInDataDirectory: true,
                DestinationExists: true);
            var operationRuntime = new BackupOperationRuntime(
                queue,
                startPlanEvidence: _ => ValueTask.FromResult(evidence),
                executeBackupAsync: archiveRuntime.CreateAsync);
            var manager = BackupManager.CreateAuthorized(
                new SevenZipBackupArchiveMetadataReader(failingExecutable),
                operationRuntime,
                dispatcher,
                authorizationGuard: static () => true);
            service = new BackupTaskHostedService(
                queue,
                NullLogger<BackupTaskHostedService>.Instance);

            await service.StartAsync(CancellationToken.None);
            manager.StartBackup();

            var failure = await dispatcher.Failure.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.AreEqual(1, dispatcher.FailureDispatchCount);
            Assert.IsFalse(failure.MetadataExistsAtDispatch);
            Assert.IsFalse(failure.DataBackupExistsAtDispatch);
            Assert.IsFalse(File.Exists(metadataPath));
            Assert.IsFalse(Directory.Exists(dataBackupPath));
            Assert.IsFalse(string.IsNullOrWhiteSpace(failure.Reason));
        }
        finally
        {
            if (service is not null)
            {
                await service.StopAsync(CancellationToken.None);
            }
        }
    }

    private sealed class RecordingBackupEventDispatcher(
        string metadataPath,
        string dataBackupPath) : IBackupEventDispatcher
    {
        internal TaskCompletionSource<FailureObservation> Failure { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int FailureDispatchCount { get; private set; }

        public void OnBackupCompleted()
        {
        }

        public void OnBackupFailed(string reason)
        {
            FailureDispatchCount++;
            Failure.TrySetResult(
                new FailureObservation(
                    reason,
                    File.Exists(metadataPath),
                    Directory.Exists(dataBackupPath)));
        }
    }

    private sealed record FailureObservation(
        string Reason,
        bool MetadataExistsAtDispatch,
        bool DataBackupExistsAtDispatch);

    private sealed class TemporaryFilesystem : IDisposable
    {
        internal TemporaryFilesystem()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                $"hmailserver-backup-failure-{Guid.NewGuid():N}");
            Directory.CreateDirectory(RootPath);
        }

        internal string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
