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
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().Backup);

        Assert.AreEqual(EAccessDenied, destinationError.ErrorCode);
        Assert.AreEqual(EAccessDenied, logFileError.ErrorCode);
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
}
