using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("C3E2DFFB-BE53-4BE6-BE57-7C5609938CEB")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceStatus
{
    [DispId(1)]
    string UndeliveredMessages { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(2)]
    string StartTime { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(3)]
    int ProcessedMessages { get; }

    [DispId(4)]
    int RemovedViruses { get; }

    [DispId(5)]
    int RemovedSpamMessages { get; }

    [DispId(6)]
    int get_SessionCount(ComSessionType iType);

    [DispId(7)]
    int ThreadID { get; }
}

[ComVisible(true)]
[Guid("ADD8B04F-F7A0-4C73-8B0B-E53B3077F052")]
[ProgId("hMailServer.Status.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceStatus))]
public sealed class Status : IInterfaceStatus
{
    private const int EAccessDenied = unchecked((int)0x80070005);

    private readonly ServerStatusSnapshot? _snapshot;

    public Status()
    {
    }

    private Status(ServerStatusSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public string UndeliveredMessages => Snapshot.UndeliveredMessages;

    public string StartTime => Snapshot.StartTime;

    public int ProcessedMessages => Snapshot.ProcessedMessages;

    public int RemovedViruses => Snapshot.RemovedViruses;

    public int RemovedSpamMessages => Snapshot.RemovedSpamMessages;

    public int ThreadID => Snapshot.ThreadID;

    internal static Status CreateAuthorized(ServerStatusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new Status(snapshot);
    }

    public int get_SessionCount(ComSessionType iType) =>
        Snapshot.GetSessionCount((int)iType);

    private ServerStatusSnapshot Snapshot =>
        _snapshot ?? throw new COMException(
            "Status access requires an authenticated process-hosted hMailServer application object.",
            EAccessDenied);
}

[ComVisible(false)]
public static class StatusAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IServerStatusAdministrationStore? _store;

    public static void Configure(IServerStatusAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static Status CreateAuthorizedAdapter()
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer status administration runtime has not been initialized.",
                CoENotInitialized);

        var snapshot = store
            .GetStatusAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return Status.CreateAuthorized(snapshot);
    }
}
