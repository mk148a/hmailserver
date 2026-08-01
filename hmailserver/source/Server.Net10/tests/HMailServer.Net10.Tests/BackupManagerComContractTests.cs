using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using BackupManagerComClass = HMailServer.ComInterop.BackupManager;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupManagerComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    [TestMethod]
    public void Interface_PreservesLegacyIidCompleteVtableAndMarshaling()
    {
        var contract = typeof(IInterfaceBackupManager);

        Assert.AreEqual(new Guid("E773E8FC-1C9A-4E96-A73C-CC02E7649637"), contract.GUID);
        Assert.AreEqual(
            ComInterfaceType.InterfaceIsDual,
            contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);
        CollectionAssert.AreEqual(
            new[] { "StartBackup", "LoadBackup" },
            contract.GetMethods()
                .OrderBy(static method => method.MetadataToken)
                .Select(static method => method.Name)
                .ToArray());

        Assert.AreEqual(1, contract.GetMethod(nameof(IInterfaceBackupManager.StartBackup))?.GetCustomAttribute<DispIdAttribute>()?.Value);
        var loadBackup = contract.GetMethod(nameof(IInterfaceBackupManager.LoadBackup));
        Assert.IsNotNull(loadBackup);
        Assert.AreEqual(2, loadBackup.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(typeof(IInterfaceBackup), loadBackup.ReturnType);
        Assert.AreEqual(UnmanagedType.BStr, loadBackup.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(new Guid("BC84454B-FCE1-41FA-A3DD-2C57F61D4310"), typeof(IInterfaceBackup).GUID);
    }

    [TestMethod]
    public void ComClass_PreservesLegacyIdentityAndDefaultInterface()
    {
        var type = typeof(BackupManagerComClass);

        Assert.AreEqual(new Guid("1BBE5234-D331-41DF-85D7-CAF0B00B3BF7"), type.GUID);
        Assert.AreEqual("hMailServer.BackupManager.1", type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(typeof(IInterfaceBackupManager), type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundaryWithoutFileAccess()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hmailserver-backup-{Guid.NewGuid():N}.xml");
        var manager = new BackupManagerComClass();

        var startError = Assert.ThrowsExactly<COMException>(manager.StartBackup);
        var loadError = Assert.ThrowsExactly<COMException>(() => manager.LoadBackup(path));
        var applicationError = Assert.ThrowsExactly<COMException>(() => _ = new Application().BackupManager);

        Assert.AreEqual(EAccessDenied, startError.ErrorCode);
        Assert.AreEqual(EAccessDenied, loadError.ErrorCode);
        Assert.AreEqual(EAccessDenied, applicationError.ErrorCode);
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void UnauthenticatedAndDirectActivation_DoNotDispatchBackupEvents()
    {
        var dispatcher = new RecordingBackupEventDispatcher();
        BackupEventDispatcherRuntimeHost.Configure(dispatcher);

        try
        {
            var manager = new BackupManagerComClass();
            var application = new Application();

            _ = Assert.ThrowsExactly<COMException>(manager.StartBackup);
            _ = Assert.ThrowsExactly<COMException>(() => _ = application.BackupManager);

            Assert.AreEqual(0, dispatcher.Events.Count);
        }
        finally
        {
            BackupEventDispatcherRuntimeHost.ResetForTests();
        }
    }

    [TestMethod]
    public void AuthorizedBackupManager_LoadsMetadataThroughInjectedReaderAndKeepsStartPending()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hmailserver-backup-{Guid.NewGuid():N}.xml");
        var reader = new RecordingBackupArchiveMetadataReader(13);
        IInterfaceBackupManager manager = BackupManagerComClass.CreateAuthorized(reader);

        AssertPending(manager.StartBackup);
        var backup = manager.LoadBackup(path);

        Assert.AreEqual(path, reader.ArchivePath);
        Assert.IsTrue(backup.ContainsSettings);
        Assert.IsFalse(backup.ContainsDomains);
        Assert.IsTrue(backup.ContainsMessages);
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void StartPlan_PreservesLegacyPreflightOrderAndNormalizedDestination()
    {
        var plan = BackupManagerComClass.CreateStartPlan(
            destination: @"D:\MailBackup\",
            backupOptions: 2 | 4 | 8,
            backupMessagesDbOnly: false,
            allMessageFilesInDataFolder: false,
            destinationExists: false);

        Assert.IsFalse(plan.CanStart);
        Assert.AreEqual(@"D:\MailBackup", plan.Destination);
        Assert.AreEqual("All messages are not located in the data folder.", plan.FailureReason);
        Assert.IsTrue(plan.IncludesMessages);
        Assert.IsTrue(plan.RequiresDataDirectoryCopy);
    }

    [TestMethod]
    public void StartPlan_RequiresMessagePlacementEvenInDatabaseOnlyMode()
    {
        var plan = BackupManagerComClass.CreateStartPlan(
            destination: @"D:\MailBackup",
            backupOptions: 4,
            backupMessagesDbOnly: true,
            allMessageFilesInDataFolder: false,
            destinationExists: true);

        Assert.IsFalse(plan.CanStart);
        Assert.AreEqual("All messages are not located in the data folder.", plan.FailureReason);
        Assert.IsFalse(plan.RequiresDataDirectoryCopy);
    }

    [TestMethod]
    public void StartPlan_ReportsNormalizedDestinationFailureWithoutFilesystemAccess()
    {
        var destination = Path.Combine(Path.GetTempPath(), $"hmailserver-backup-plan-{Guid.NewGuid():N}") + "\\";
        var plan = BackupManagerComClass.CreateStartPlan(
            destination,
            backupOptions: 0,
            backupMessagesDbOnly: false,
            allMessageFilesInDataFolder: true,
            destinationExists: false);

        Assert.IsFalse(plan.CanStart);
        Assert.AreEqual(destination[..^1], plan.Destination);
        Assert.AreEqual(
            "The specified backup directory is not accessible: " + destination[..^1],
            plan.FailureReason);
        Assert.IsFalse(Directory.Exists(destination[..^1]));
    }

    [TestMethod]
    public void StartPlan_AllowsConfiguredOptionsWhenLegacyPreconditionsPass()
    {
        var plan = BackupManagerComClass.CreateStartPlan(
            destination: @"D:\MailBackup\",
            backupOptions: 1 | 2 | 4 | 8,
            backupMessagesDbOnly: false,
            allMessageFilesInDataFolder: true,
            destinationExists: true);

        Assert.IsTrue(plan.CanStart);
        Assert.IsNull(plan.FailureReason);
        Assert.AreEqual(@"D:\MailBackup", plan.Destination);
        Assert.IsTrue(plan.IncludesMessages);
        Assert.IsTrue(plan.RequiresDataDirectoryCopy);
    }

    [TestMethod]
    public void AuthorizedStartBackup_QueuesOnceUntilThreadStops()
    {
        var enqueueCount = 0;
        var coordinator = new BackupOperationCoordinator(
            _ =>
            {
                enqueueCount++;
                return true;
            });
        IInterfaceBackupManager manager = BackupManagerComClass.CreateAuthorized(
            new RecordingBackupArchiveMetadataReader(0),
            coordinator);

        manager.StartBackup();
        manager.StartBackup();

        Assert.AreEqual(1, enqueueCount);
        Assert.IsTrue(coordinator.IsRunning);

        coordinator.OnThreadStopped();
        manager.StartBackup();

        Assert.AreEqual(2, enqueueCount);
        Assert.IsTrue(coordinator.IsRunning);
    }

    [TestMethod]
    public void AuthorizedStartBackup_DuplicateStartDispatchesFailureReason()
    {
        var dispatcher = new RecordingBackupEventDispatcher();
        var coordinator = new BackupOperationCoordinator(_ => true);
        var manager = BackupManagerComClass.CreateAuthorized(
            new RecordingBackupArchiveMetadataReader(0),
            coordinator,
            dispatcher);

        manager.StartBackup();
        manager.StartBackup();

        CollectionAssert.AreEqual(
            new[] { "failed:Backup or restore operation is already started" },
            dispatcher.Events.ToArray());
    }

    [TestMethod]
    public void AuthorizedStartBackup_ResetsStateWhenMaintenanceQueueIsUnavailable()
    {
        var enqueueCount = 0;
        var coordinator = new BackupOperationCoordinator(
            _ =>
            {
                enqueueCount++;
                return false;
            });
        IInterfaceBackupManager manager = BackupManagerComClass.CreateAuthorized(
            new RecordingBackupArchiveMetadataReader(0),
            coordinator);

        manager.StartBackup();
        manager.StartBackup();

        Assert.AreEqual(2, enqueueCount);
        Assert.IsFalse(coordinator.IsRunning);
    }

    [TestMethod]
    public void AuthorizedStartBackup_QueueUnavailableDispatchesFailureReason()
    {
        var dispatcher = new RecordingBackupEventDispatcher();
        var coordinator = new BackupOperationCoordinator(_ => false);
        var manager = BackupManagerComClass.CreateAuthorized(
            new RecordingBackupArchiveMetadataReader(0),
            coordinator,
            dispatcher);

        manager.StartBackup();

        CollectionAssert.AreEqual(
            new[] { "failed:Backup operation failed because random work queue did not exist." },
            dispatcher.Events.ToArray());
    }

    [TestMethod]
    public void ScriptBackupEventDispatcher_PreservesLegacyEventNamesAndFailureReason()
    {
        var executor = new RecordingBackupEventScriptExecutor();
        var dispatcher = new ScriptBackupEventDispatcher(executor);

        dispatcher.OnBackupCompleted();
        dispatcher.OnBackupFailed("could not write archive");

        CollectionAssert.AreEqual(
            new[]
            {
                "OnBackupCompleted:",
                "OnBackupFailed:could not write archive"
            },
            executor.Events.ToArray());
    }

    [TestMethod]
    public void ScriptBackupEventDispatcher_ReportsRejectedScriptExecution()
    {
        var dispatcher = new ScriptBackupEventDispatcher(
            new RejectingBackupEventScriptExecutor("script process failed"));

        var error = Assert.ThrowsExactly<InvalidOperationException>(
            dispatcher.OnBackupCompleted);

        Assert.AreEqual("script process failed", error.Message);
    }

    [TestMethod]
    public void AuthenticatedApplication_UsesConfiguredBackupOperationRuntime()
    {
        var enqueueCount = 0;
        var coordinator = new BackupOperationCoordinator(
            _ =>
            {
                enqueueCount++;
                return true;
            });
        BackupManagerRuntimeHost.Configure(coordinator);

        try
        {
            var application = new Application(
                new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"),
                new RecordingBackupArchiveMetadataReader(0));

            Assert.IsNotNull(application.Authenticate("administrator", "secret"));
            application.BackupManager.StartBackup();

            Assert.AreEqual(1, enqueueCount);
            Assert.IsTrue(coordinator.IsRunning);
        }
        finally
        {
            BackupManagerRuntimeHost.ResetForTests();
        }
    }

    [TestMethod]
    public async Task QueuedBackupTask_CarriesStatusFailureCompletionAndThreadStopCallbacks()
    {
        using var queue = new BackupTaskQueue();
        var runtime = new BackupOperationRuntime(queue);
        var dispatcher = new RecordingBackupEventDispatcher();
        var manager = BackupManagerComClass.CreateAuthorized(
            new RecordingBackupArchiveMetadataReader(0),
            runtime,
            dispatcher);

        manager.StartBackup();

        await using var reader = queue
            .ReadAllAsync(CancellationToken.None)
            .GetAsyncEnumerator();
        Assert.IsTrue(await reader.MoveNextAsync());

        var task = reader.Current;
        task.SetStatus("Loading backup settings....");
        task.Failed("Backup execution is not implemented.");
        task.Completed();
        task.ThreadStopped();

        StringAssert.Contains(
            manager.GetStatus(),
            "Backup started\r\nLoading backup settings....\r\nBACKUP ERROR: Backup execution is not implemented.\r\nBackup completed successfully\r\n");
        CollectionAssert.AreEqual(
            new[]
            {
                "failed:Backup execution is not implemented.",
                "completed"
            },
            dispatcher.Events.ToArray());
    }

    [TestMethod]
    public void AuthenticatedApplication_ExposesAuthorizedBackupManagerChild()
    {
        var reader = new RecordingBackupArchiveMetadataReader(2);
        var application = new Application(
            new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"),
            reader);

        var denied = Assert.ThrowsExactly<COMException>(() => _ = application.BackupManager);
        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.IsNull(application.Authenticate("administrator", "wrong"));
        Assert.IsNotNull(application.Authenticate("administrator", "secret"));

        var manager = application.BackupManager;

        Assert.IsInstanceOfType<BackupManagerComClass>(manager);
        AssertPending(manager.StartBackup);
        Assert.IsTrue(manager.LoadBackup(@"D:\Backups\sample.7z").ContainsDomains);
        Assert.AreEqual(@"D:\Backups\sample.7z", reader.ArchivePath);
    }

    private static void AssertPending(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);

        Assert.AreEqual(ENotImplemented, error.ErrorCode);
    }

    private sealed class RecordingBackupArchiveMetadataReader(int options) : IBackupArchiveMetadataReader
    {
        public string? ArchivePath { get; private set; }

        public int ReadContainsOptions(string archivePath)
        {
            ArchivePath = archivePath;
            return options;
        }
    }

    private sealed class RecordingBackupEventDispatcher : IBackupEventDispatcher
    {
        public List<string> Events { get; } = [];

        public void OnBackupCompleted() => Events.Add("completed");

        public void OnBackupFailed(string reason) => Events.Add("failed:" + reason);
    }

    private sealed class RecordingBackupEventScriptExecutor : IBackupEventScriptExecutor
    {
        public List<string> Events { get; } = [];

        public SmtpRuleScriptExecutionResult Execute(
            BackupEventScriptExecutionRequest request,
            CancellationToken cancellationToken)
        {
            Events.Add(request.EventName + ":" + request.FailureReason);
            return SmtpRuleScriptExecutionResult.Continue();
        }
    }

    private sealed class RejectingBackupEventScriptExecutor(string message)
        : IBackupEventScriptExecutor
    {
        public SmtpRuleScriptExecutionResult Execute(
            BackupEventScriptExecutionRequest request,
            CancellationToken cancellationToken) =>
            SmtpRuleScriptExecutionResult.Failure(message);
    }
}
