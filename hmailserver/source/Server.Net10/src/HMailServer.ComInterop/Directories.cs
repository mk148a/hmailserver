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
    private readonly Func<string, bool>? _updateTempDirectory;
    private readonly Func<string, bool>? _updateDataDirectory;
    private readonly Func<string, bool>? _updateProgramDirectory;
    private readonly Func<string, bool>? _updateEventDirectory;

    public Directories()
    {
    }

    private Directories(
        DirectoryAdministrationSnapshot snapshot,
        Func<string, bool>? updateLogDirectory,
        Func<string, bool>? updateTempDirectory,
        Func<string, bool>? updateDataDirectory,
        Func<string, bool>? updateProgramDirectory,
        Func<string, bool>? updateEventDirectory)
    {
        _snapshot = snapshot;
        _updateLogDirectory = updateLogDirectory;
        _updateTempDirectory = updateTempDirectory;
        _updateDataDirectory = updateDataDirectory;
        _updateProgramDirectory = updateProgramDirectory;
        _updateEventDirectory = updateEventDirectory;
    }

    public string ProgramDirectory
    {
        get => Snapshot.ProgramDirectory;
        set
        {
            _ = Snapshot;
            var updateProgramDirectory = _updateProgramDirectory;
            if (updateProgramDirectory is null)
            {
                Unavailable();
                return;
            }

            if (!updateProgramDirectory(value))
            {
                throw new COMException(
                    "The program directory update could not be persisted.",
                    unchecked((int)0x80004005));
            }

            // Legacy SetProgramDirectory does not rebuild the cached DB script path.
            _snapshot = Snapshot with { ProgramDirectory = value };
        }
    }

    public string DatabaseDirectory { get => Snapshot.DatabaseDirectory; set => Unavailable(); }

    public string DataDirectory
    {
        get => Snapshot.DataDirectory;
        set
        {
            _ = Snapshot;
            var updateDataDirectory = _updateDataDirectory;
            if (updateDataDirectory is null)
            {
                Unavailable();
                return;
            }

            if (!updateDataDirectory(value))
            {
                throw new COMException(
                    "The data directory update could not be persisted.",
                    unchecked((int)0x80004005));
            }

            _snapshot = Snapshot with { DataDirectory = value };
        }
    }

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

    public string TempDirectory
    {
        get => Snapshot.TempDirectory;
        set
        {
            _ = Snapshot;
            var updateTempDirectory = _updateTempDirectory;
            if (updateTempDirectory is null)
            {
                Unavailable();
                return;
            }

            if (!updateTempDirectory(value))
            {
                throw new COMException(
                    "The temp directory update could not be persisted.",
                    unchecked((int)0x80004005));
            }

            _snapshot = Snapshot with { TempDirectory = value };
        }
    }

    public string EventDirectory
    {
        get => Snapshot.EventDirectory;
        set
        {
            _ = Snapshot;
            var updateEventDirectory = _updateEventDirectory;
            if (updateEventDirectory is null)
            {
                Unavailable();
                return;
            }

            if (!updateEventDirectory(value))
            {
                throw new COMException(
                    "The event directory update could not be persisted.",
                    unchecked((int)0x80004005));
            }

            _snapshot = Snapshot with { EventDirectory = value };
        }
    }

    public string DBScriptDirectory => Snapshot.DBScriptDirectory;

    internal static Directories CreateAuthorized(
        DirectoryAdministrationSnapshot snapshot,
        Func<string, bool>? updateLogDirectory = null,
        Func<string, bool>? updateTempDirectory = null,
        Func<string, bool>? updateDataDirectory = null,
        Func<string, bool>? updateProgramDirectory = null,
        Func<string, bool>? updateEventDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new Directories(snapshot, updateLogDirectory, updateTempDirectory, updateDataDirectory, updateProgramDirectory, updateEventDirectory);
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
                .GetResult(),
            value => store
                .UpdateTempDirectoryAsync(value, CancellationToken.None)
                .GetAwaiter()
                .GetResult(),
            value => store
                .UpdateDataDirectoryAsync(value, CancellationToken.None)
                .GetAwaiter()
                .GetResult(),
            value => store
                .UpdateProgramDirectoryAsync(value, CancellationToken.None)
                .GetAwaiter()
                .GetResult(),
            value => store
                .UpdateEventDirectoryAsync(value, CancellationToken.None)
                .GetAwaiter()
                .GetResult());
    }
}
