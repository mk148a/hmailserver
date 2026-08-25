using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DirectoriesComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    [TestMethod]
    public void Interface_PreservesLegacyIidDispatchIdsAndCompleteVtableOrder()
    {
        Assert.AreEqual(
            new Guid("48B534F3-2C4E-47F6-8CB0-339676B0ABF3"),
            typeof(IInterfaceDirectories).GUID);
        Assert.AreEqual(
            ComInterfaceType.InterfaceIsDual,
            typeof(IInterfaceDirectories).GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            typeof(IInterfaceDirectories).GetCustomAttribute<TypeLibTypeAttribute>()?.Value);

        CollectionAssert.AreEqual(
            new[]
            {
                "get_ProgramDirectory",
                "set_ProgramDirectory",
                "get_DatabaseDirectory",
                "set_DatabaseDirectory",
                "get_DataDirectory",
                "set_DataDirectory",
                "get_LogDirectory",
                "set_LogDirectory",
                "get_TempDirectory",
                "set_TempDirectory",
                "get_EventDirectory",
                "set_EventDirectory",
                "get_DBScriptDirectory"
            },
            typeof(IInterfaceDirectories)
                .GetMethods()
                .OrderBy(static method => method.MetadataToken)
                .Select(static method => method.Name)
                .ToArray());

        Assert.AreEqual(
            1,
            typeof(IInterfaceDirectories).GetProperty(nameof(IInterfaceDirectories.ProgramDirectory))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            7,
            typeof(IInterfaceDirectories).GetProperty(nameof(IInterfaceDirectories.DBScriptDirectory))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClass_PreservesLegacyIdentityAndDefaultInterface()
    {
        var type = typeof(Directories);

        Assert.AreEqual(new Guid("1969A4DF-B1B0-4A71-8196-5FD392CA3D8A"), type.GUID);
        Assert.AreEqual("hMailServer.Directories.1", type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(typeof(IInterfaceDirectories), type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var directoriesError = Assert.ThrowsExactly<COMException>(() => _ = new Directories().ProgramDirectory);
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().Directories);

        Assert.AreEqual(EAccessDenied, directoriesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, settingsError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedDirectories_ExposeReadOnlySnapshotAndKeepMutationsPending()
    {
        IInterfaceDirectories directories = Directories.CreateAuthorized(
            new DirectoryAdministrationSnapshot(
                ProgramDirectory: @"C:\hMailServer\",
                DatabaseDirectory: @"C:\hMailServer\Database",
                DataDirectory: @"C:\hMailServer\Data",
                LogDirectory: @"C:\hMailServer\Logs\",
                TempDirectory: @"C:\hMailServer\Temp",
                EventDirectory: @"C:\hMailServer\Events\",
                DBScriptDirectory: @"C:\hMailServer\DBScripts"));

        Assert.AreEqual(@"C:\hMailServer\", directories.ProgramDirectory);
        Assert.AreEqual(@"C:\hMailServer\Database", directories.DatabaseDirectory);
        Assert.AreEqual(@"C:\hMailServer\Data", directories.DataDirectory);
        Assert.AreEqual(@"C:\hMailServer\Logs\", directories.LogDirectory);
        Assert.AreEqual(@"C:\hMailServer\Temp", directories.TempDirectory);
        Assert.AreEqual(@"C:\hMailServer\Events\", directories.EventDirectory);
        Assert.AreEqual(@"C:\hMailServer\DBScripts", directories.DBScriptDirectory);

        AssertMutationPending(() => directories.ProgramDirectory = @"D:\Program");
        AssertMutationPending(() => directories.DatabaseDirectory = @"D:\Database");
        AssertMutationPending(() => directories.DataDirectory = @"D:\Data");
        AssertMutationPending(() => directories.LogDirectory = @"D:\Logs");
        AssertMutationPending(() => directories.TempDirectory = @"D:\Temp");
        AssertMutationPending(() => directories.EventDirectory = @"D:\Events");
    }

    [TestMethod]
    public void AuthorizedSettings_UsesConfiguredDirectoryRuntime()
    {
        DirectoryAdministrationRuntimeHost.Configure(
            new FixedDirectoryAdministrationStore(
                new DirectoryAdministrationSnapshot(
                    ProgramDirectory: @"E:\hMailServer\",
                    DatabaseDirectory: @"E:\hMailServer\Database",
                    DataDirectory: @"E:\hMailServer\Data",
                    LogDirectory: @"E:\hMailServer\Logs",
                    TempDirectory: @"E:\hMailServer\Temp",
                    EventDirectory: @"E:\hMailServer\Events",
                    DBScriptDirectory: @"E:\hMailServer\DBScripts")));
        IInterfaceSettings settings = Settings.CreateAuthorized();

        var directories = settings.Directories;

        Assert.AreEqual(@"E:\hMailServer\", directories.ProgramDirectory);
        Assert.AreEqual(@"E:\hMailServer\Data", directories.DataDirectory);
        Assert.AreEqual(@"E:\hMailServer\DBScripts", directories.DBScriptDirectory);
    }

    [TestMethod]
    public void AuthorizedRuntime_LogDirectorySetterPersistsAndRefreshesRetainedObject()
    {
        var iniPath = Path.Combine(Path.GetTempPath(), $"hmailserver-{Guid.NewGuid():N}.ini");
        try
        {
            File.WriteAllText(iniPath, "[Directories]\r\nLogFolder=C:\\hMailServer\\Logs\r\nProgramFolder=C:\\hMailServer\\\r\n");
            DirectoryAdministrationRuntimeHost.Configure(new LegacyDirectoryAdministrationStore(iniPath));
            var directories = Settings.CreateAuthorized().Directories;

            directories.LogDirectory = @"D:\Logs";

            Assert.AreEqual(@"D:\Logs", directories.LogDirectory);
            StringAssert.Contains(File.ReadAllText(iniPath), "LogFolder=D:\\Logs");
        }
        finally
        {
            if (File.Exists(iniPath))
            {
                File.Delete(iniPath);
            }
        }
    }

    private static void AssertMutationPending(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);

        Assert.AreEqual(ENotImplemented, error.ErrorCode);
    }

    private sealed class FixedDirectoryAdministrationStore(DirectoryAdministrationSnapshot snapshot)
        : IDirectoryAdministrationStore
    {
        public ValueTask<DirectoryAdministrationSnapshot> GetDirectoriesAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(snapshot);
    }
}
