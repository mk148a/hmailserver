using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using BackupSettingsComClass = HMailServer.ComInterop.BackupSettings;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupSettingsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    [TestMethod]
    public void Interface_PreservesLegacyIidCompleteVtableAndMarshaling()
    {
        var contract = typeof(IInterfaceBackupSettings);

        Assert.AreEqual(new Guid("2C5559F0-DF3F-43C0-935C-F79D41CF8A5B"), contract.GUID);
        Assert.AreEqual(
            ComInterfaceType.InterfaceIsDual,
            contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);
        CollectionAssert.AreEqual(
            new[]
            {
                "get_Destination",
                "set_Destination",
                "get_BackupSettings",
                "set_BackupSettings",
                "get_BackupDomains",
                "set_BackupDomains",
                "get_BackupMessages",
                "set_BackupMessages",
                "get_CompressDestinationFiles",
                "set_CompressDestinationFiles",
                "get_LogFile"
            },
            contract.GetMethods()
                .OrderBy(static method => method.MetadataToken)
                .Select(static method => method.Name)
                .ToArray());

        AssertBstrProperty(contract, nameof(IInterfaceBackupSettings.Destination), 1, canWrite: true);
        AssertVariantBoolProperty(contract, nameof(IInterfaceBackupSettings.BackupSettings), 2);
        AssertVariantBoolProperty(contract, nameof(IInterfaceBackupSettings.BackupDomains), 3);
        AssertVariantBoolProperty(contract, nameof(IInterfaceBackupSettings.BackupMessages), 4);
        AssertVariantBoolProperty(contract, nameof(IInterfaceBackupSettings.CompressDestinationFiles), 5);
        AssertBstrProperty(contract, nameof(IInterfaceBackupSettings.LogFile), 7, canWrite: false);
    }

    [TestMethod]
    public void ComClass_PreservesLegacyIdentityAndDefaultInterface()
    {
        var type = typeof(BackupSettingsComClass);

        Assert.AreEqual(new Guid("E0213ECF-BAEC-4E20-9813-0F75A97D0B16"), type.GUID);
        Assert.AreEqual("hMailServer.BackupSettings.1", type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(typeof(IInterfaceBackupSettings), type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var destinationError = Assert.ThrowsExactly<COMException>(() => _ = new BackupSettingsComClass().Destination);
        var logFileError = Assert.ThrowsExactly<COMException>(() => _ = new BackupSettingsComClass().LogFile);
        var backupSettingsError = Assert.ThrowsExactly<COMException>(() => _ = new BackupSettingsComClass().BackupSettings);
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().Backup);

        Assert.AreEqual(EAccessDenied, destinationError.ErrorCode);
        Assert.AreEqual(EAccessDenied, logFileError.ErrorCode);
        Assert.AreEqual(EAccessDenied, backupSettingsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, settingsError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedBackupSettings_ExposesLegacyOptionBitsAndKeepsSettersPending()
    {
        IInterfaceBackupSettings backup = BackupSettingsComClass.CreateAuthorized(
            new BackupSettingsAdministrationSnapshot(
                Destination: @"D:\Backups",
                Options: 13,
                LogDirectory: @"C:\hMailServer\Logs"));

        Assert.AreEqual(@"D:\Backups", backup.Destination);
        Assert.IsTrue(backup.BackupSettings);
        Assert.IsFalse(backup.BackupDomains);
        Assert.IsTrue(backup.BackupMessages);
        Assert.IsTrue(backup.CompressDestinationFiles);
        Assert.AreEqual(@"C:\hMailServer\Logs\hmailserver_backup.log", backup.LogFile);

        AssertPending(() => backup.Destination = @"E:\Other");
        AssertPending(() => backup.BackupSettings = false);
        AssertPending(() => backup.BackupDomains = true);
        AssertPending(() => backup.BackupMessages = false);
        AssertPending(() => backup.CompressDestinationFiles = false);
    }

    [TestMethod]
    public void AuthorizedBackupSettings_DestinationSetterPersistsAndUpdatesSnapshot()
    {
        var updateCount = 0;
        var persistedDestination = string.Empty;
        var backup = BackupSettingsComClass.CreateAuthorized(
            new BackupSettingsAdministrationSnapshot(
                Destination: @"D:\Backups",
                Options: 0,
                LogDirectory: string.Empty),
            updateDestination: value =>
            {
                updateCount++;
                persistedDestination = value;
                return true;
            });

        backup.Destination = @"E:\Other";

        Assert.AreEqual(1, updateCount);
        Assert.AreEqual(@"E:\Other", persistedDestination);
        Assert.AreEqual(@"E:\Other", backup.Destination);
    }

    [TestMethod]
    public void AuthorizedBackupSettings_DestinationSetterFailureRetainsSnapshot()
    {
        var updateCount = 0;
        var backup = BackupSettingsComClass.CreateAuthorized(
            new BackupSettingsAdministrationSnapshot(
                Destination: @"D:\Backups",
                Options: 0,
                LogDirectory: string.Empty),
            updateDestination: _ =>
            {
                updateCount++;
                return false;
            });

        var error = Assert.ThrowsExactly<COMException>(() => backup.Destination = @"E:\Other");

        Assert.AreEqual(unchecked((int)0x80004005), error.ErrorCode);
        Assert.AreEqual(1, updateCount);
        Assert.AreEqual(@"D:\Backups", backup.Destination);
    }

    [TestMethod]
    public void AuthorizedBackupSettings_DestinationSetterRetainsAdministratorDenial()
    {
        var updateCount = 0;
        var backup = BackupSettingsComClass.CreateAuthorized(
            new BackupSettingsAdministrationSnapshot(
                Destination: @"D:\Backups",
                Options: 0,
                LogDirectory: string.Empty),
            updateDestination: _ =>
            {
                updateCount++;
                return true;
            },
            isServerAdministrator: static () => false);

        var error = Assert.ThrowsExactly<COMException>(() => backup.Destination = @"E:\Other");

        Assert.AreEqual(EAccessDenied, error.ErrorCode);
        Assert.AreEqual(0, updateCount);
        Assert.AreEqual(@"D:\Backups", backup.Destination);
    }

    [TestMethod]
    public void AuthorizedBackupSettings_BackupSettingsSetterTransitionsBitOneAndPreservesOtherBits()
    {
        var persisted = new List<bool>();
        var published = new List<int>();
        var backup = BackupSettingsComClass.CreateAuthorized(
            new BackupSettingsAdministrationSnapshot(@"D:\Backups", 14, string.Empty),
            updateBackupSettings: value =>
            {
                persisted.Add(value);
                return true;
            },
            backupSettingsUpdated: published.Add,
            isServerAdministrator: static () => true,
            authorizationLeaseFactory: static _ =>
                ValueTask.FromResult<IDisposable?>(new TrackingLease()));

        backup.BackupSettings = true;
        Assert.IsTrue(backup.BackupSettings);
        Assert.AreEqual(15, published[0]);

        backup.BackupSettings = false;
        Assert.IsFalse(backup.BackupSettings);
        CollectionAssert.AreEqual(new[] { true, false }, persisted);
        CollectionAssert.AreEqual(new[] { 15, 14 }, published);
    }

    [TestMethod]
    public void AuthorizedBackupSettings_BackupSettingsSetterFailureDoesNotPublishSnapshot()
    {
        var publishCount = 0;
        var backup = BackupSettingsComClass.CreateAuthorized(
            new BackupSettingsAdministrationSnapshot(@"D:\Backups", 15, string.Empty),
            updateBackupSettings: static _ => false,
            backupSettingsUpdated: _ => publishCount++,
            isServerAdministrator: static () => true,
            authorizationLeaseFactory: static _ =>
                ValueTask.FromResult<IDisposable?>(new TrackingLease()));

        var error = Assert.ThrowsExactly<COMException>(() => backup.BackupSettings = false);

        Assert.AreEqual(unchecked((int)0x80004005), error.ErrorCode);
        Assert.AreEqual(0, publishCount);
        Assert.IsTrue(backup.BackupSettings);
    }

    [TestMethod]
    public void AuthorizedBackupSettings_BackupSettingsSetterDeniesAdministratorOrExpiredLeaseBeforeStore()
    {
        var updateCount = 0;
        var backup = BackupSettingsComClass.CreateAuthorized(
            new BackupSettingsAdministrationSnapshot(@"D:\Backups", 14, string.Empty),
            updateBackupSettings: _ =>
            {
                updateCount++;
                return true;
            },
            isServerAdministrator: static () => false,
            authorizationLeaseFactory: static _ =>
                ValueTask.FromResult<IDisposable?>(new TrackingLease()));

        var administratorError = Assert.ThrowsExactly<COMException>(() => backup.BackupSettings = true);

        Assert.AreEqual(EAccessDenied, administratorError.ErrorCode);
        Assert.AreEqual(0, updateCount);

        backup = BackupSettingsComClass.CreateAuthorized(
            new BackupSettingsAdministrationSnapshot(@"D:\Backups", 14, string.Empty),
            updateBackupSettings: _ =>
            {
                updateCount++;
                return true;
            },
            isServerAdministrator: static () => true,
            authorizationLeaseFactory: static _ => ValueTask.FromResult<IDisposable?>(null));

        var leaseError = Assert.ThrowsExactly<COMException>(() => backup.BackupSettings = true);

        Assert.AreEqual(EAccessDenied, leaseError.ErrorCode);
        Assert.AreEqual(0, updateCount);
    }

    [TestMethod]
    public void AuthorizedBackupSettings_LogFilePreservesLegacySeparatorRulesWithoutFileAccess()
    {
        var missingDirectory = Path.Combine(Path.GetTempPath(), $"hmailserver-backup-{Guid.NewGuid():N}");
        var cases = new[]
        {
            (missingDirectory, missingDirectory + @"\hmailserver_backup.log"),
            (missingDirectory + @"\", missingDirectory + @"\hmailserver_backup.log"),
            (missingDirectory + "/", missingDirectory + @"/\hmailserver_backup.log"),
            (string.Empty, @"\hmailserver_backup.log")
        };

        Assert.IsFalse(Directory.Exists(missingDirectory));

        foreach (var (directory, expected) in cases)
        {
            var backup = BackupSettingsComClass.CreateAuthorized(
                new BackupSettingsAdministrationSnapshot(string.Empty, 0, directory));

            Assert.AreEqual(expected, backup.LogFile);
        }

        Assert.IsFalse(Directory.Exists(missingDirectory));
    }

    [TestMethod]
    public void AuthorizedSettings_ExposesConfiguredBackupSnapshot()
    {
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                BackupDestination: @"E:\MailBackup",
                BackupOptions: 2),
            new SettingsRuntimeConfiguration(
                LoggingDirectory: @"E:\hMailServer\Logs\"));

        var backup = settings.Backup;

        Assert.AreEqual(@"E:\MailBackup", backup.Destination);
        Assert.IsFalse(backup.BackupSettings);
        Assert.IsTrue(backup.BackupDomains);
        Assert.IsFalse(backup.BackupMessages);
        Assert.IsFalse(backup.CompressDestinationFiles);
        Assert.AreEqual(@"E:\hMailServer\Logs\hmailserver_backup.log", backup.LogFile);
    }

    [TestMethod]
    public void AdministratorBackupSaveWritesDestinationAfterPendingOptionSetters()
    {
        var source = ReadAdministratorBackupSource();
        var optionsPosition = source.IndexOf("backupSettings.BackupSettings =", StringComparison.Ordinal);
        var destinationPosition = source.IndexOf("backupSettings.Destination =", StringComparison.Ordinal);

        Assert.IsTrue(optionsPosition >= 0, "The Administrator backup option writes were not found.");
        Assert.IsTrue(destinationPosition > optionsPosition, "Destination must be written after the option setters.");
    }

    private static void AssertBstrProperty(Type contract, string name, int dispatchId, bool canWrite)
    {
        var property = contract.GetProperty(name);

        Assert.IsNotNull(property);
        Assert.AreEqual(dispatchId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.BStr, property.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(canWrite, property.CanWrite);
        if (canWrite)
        {
            Assert.AreEqual(UnmanagedType.BStr, property.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        }
    }

    private static void AssertVariantBoolProperty(Type contract, string name, int dispatchId)
    {
        var property = contract.GetProperty(name);

        Assert.IsNotNull(property);
        Assert.AreEqual(dispatchId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.VariantBool, property.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.VariantBool, property.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
    }

    private static void AssertPending(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);

        Assert.AreEqual(ENotImplemented, error.ErrorCode);
    }

    private sealed class TrackingLease : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private static string ReadAdministratorBackupSource()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var path = Path.Combine(
                directory.FullName,
                "hmailserver",
                "source",
                "Tools",
                "Administrator",
                "Main panes",
                "ucBackup.cs");

            if (File.Exists(path))
                return File.ReadAllText(path);
        }

        Assert.Fail("Could not locate the Administrator backup pane source.");
        return string.Empty;
    }
}
