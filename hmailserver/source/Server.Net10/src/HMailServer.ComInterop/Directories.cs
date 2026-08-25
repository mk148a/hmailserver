using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("48B534F3-2C4E-47F6-8CB0-339676B0ABF3")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceDirectories
{
    [DispId(1)]
    string ProgramDirectory { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(2)]
    string DatabaseDirectory { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(3)]
    string DataDirectory { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(4)]
    string LogDirectory { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(5)]
    string TempDirectory { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(6)]
    string EventDirectory { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(7)]
    string DBScriptDirectory { [return: MarshalAs(UnmanagedType.BStr)] get; }
}

[ComVisible(true)]
[Guid("1969A4DF-B1B0-4A71-8196-5FD392CA3D8A")]
[ProgId("hMailServer.Directories.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDirectories))]
public sealed class Directories : IInterfaceDirectories
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private DirectoryAdministrationSnapshot? _snapshot;
    private readonly Func<string, bool>? _updateLogDirectory;

    public Directories()
    {
    }

    private Directories(
        DirectoryAdministrationSnapshot snapshot,
        Func<string, bool>? updateLogDirectory)
    {
        _snapshot = snapshot;
        _updateLogDirectory = updateLogDirectory;
    }

    public string ProgramDirectory { get => Snapshot.ProgramDirectory; set => Unavailable(); }

    public string DatabaseDirectory { get => Snapshot.DatabaseDirectory; set => Unavailable(); }

    public string DataDirectory { get => Snapshot.DataDirectory; set => Unavailable(); }

    public string LogDirectory
    {
        get => Snapshot.LogDirectory;
        set
        {
            _ = Snapshot;
            var updateLogDirectory = _updateLogDirectory;
            if (updateLogDirectory is null)
            {
                Unavailable();
                return;
            }

            if (!updateLogDirectory(value))
            {
                throw new COMException(
                    "The log directory update could not be persisted.",
                    unchecked((int)0x80004005));
            }

            _snapshot = Snapshot with { LogDirectory = value };
        }
    }

    public string TempDirectory { get => Snapshot.TempDirectory; set => Unavailable(); }

    public string EventDirectory { get => Snapshot.EventDirectory; set => Unavailable(); }

    public string DBScriptDirectory => Snapshot.DBScriptDirectory;

    internal static Directories CreateAuthorized(
        DirectoryAdministrationSnapshot snapshot,
        Func<string, bool>? updateLogDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new Directories(snapshot, updateLogDirectory);
    }

    private DirectoryAdministrationSnapshot Snapshot =>
        _snapshot ?? throw new COMException(
            "Directories access requires an authenticated server administrator.",
            EAccessDenied);

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This Directories member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class DirectoryAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IDirectoryAdministrationStore? _store;

    public static void Configure(IDirectoryAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static Directories CreateAuthorizedAdapter()
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer directory administration runtime has not been initialized.",
                CoENotInitialized);

        var snapshot = store
            .GetDirectoriesAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return Directories.CreateAuthorized(
            snapshot,
            value => store
                .UpdateLogDirectoryAsync(value, CancellationToken.None)
                .GetAwaiter()
                .GetResult());
    }
}
