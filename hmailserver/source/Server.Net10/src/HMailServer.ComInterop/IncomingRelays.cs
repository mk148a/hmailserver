using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("49D48933-3219-4D7E-84D5-B26FE5F0E165")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceIncomingRelays
{
    [DispId(0)]
    IInterfaceIncomingRelay this[int index] { get; }

    [DispId(1)]
    [SpecialName]
    IInterfaceIncomingRelay get_ItemByDBID(int databaseId);

    [DispId(2)]
    void Delete(int index);

    [DispId(3)]
    void DeleteByDBID(int databaseId);

    [DispId(4)]
    void Refresh();

    [DispId(5)]
    IInterfaceIncomingRelay Add();

    [DispId(6)]
    int Count { get; }

    [DispId(7)]
    [SpecialName]
    IInterfaceIncomingRelay get_ItemByName([MarshalAs(UnmanagedType.BStr)] string itemName);
}

[ComVisible(true)]
[Guid("088D748B-7CCE-4B8D-A103-D99DA83775AB")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceIncomingRelay
{
    [DispId(0)]
    int ID { get; }

    [DispId(1)]
    string LowerIP { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(2)]
    string UpperIP { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(3)]
    string Name { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(4)]
    void Delete();

    [DispId(5)]
    void Save();
}

[ComVisible(true)]
[Guid("3E75EE53-EAA6-40A5-B2CE-9CB8D7EE9278")]
[ProgId("hMailServer.IncomingRelays.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceIncomingRelays))]
public sealed class IncomingRelays : IInterfaceIncomingRelays
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private IncomingRelayAdministrationSnapshot[]? _relays;
    private readonly Func<IReadOnlyList<IncomingRelayAdministrationSnapshot>>? _reload;
    private readonly Action<int>? _deleteById;

    public IncomingRelays()
    {
    }

    private IncomingRelays(
        IReadOnlyList<IncomingRelayAdministrationSnapshot> relays,
        Func<IReadOnlyList<IncomingRelayAdministrationSnapshot>>? reload,
        Action<int>? deleteById)
    {
        _relays = relays.ToArray();
        _reload = reload;
        _deleteById = deleteById;
    }

    public int Count => GetRelays().Count;

    internal static IncomingRelays CreateAuthorized(
        IReadOnlyList<IncomingRelayAdministrationSnapshot> relays,
        Func<IReadOnlyList<IncomingRelayAdministrationSnapshot>>? reload = null,
        Action<int>? deleteById = null)
    {
        ArgumentNullException.ThrowIfNull(relays);
        return new IncomingRelays(relays, reload, deleteById);
    }

    public IInterfaceIncomingRelay this[int index]
    {
        get
        {
            var relays = GetRelays();
            if (index < 0 || index >= relays.Count)
            {
                throw new COMException("Incoming relay index was outside the collection.", DispEBadIndex);
            }

            return IncomingRelay.CreateAuthorized(relays[index]);
        }
    }

    public IInterfaceIncomingRelay get_ItemByDBID(int databaseId)
    {
        var match = GetRelays().FirstOrDefault(relay => relay.Id == databaseId);

        return match is null
            ? throw new COMException("No incoming relay with the specified database identifier exists.", DispEBadIndex)
            : IncomingRelay.CreateAuthorized(match);
    }

    public IInterfaceIncomingRelay get_ItemByName(string itemName)
    {
        var match = GetRelays().FirstOrDefault(
            relay => string.Equals(relay.Name, itemName, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No incoming relay with the specified name exists.", DispEBadIndex)
            : IncomingRelay.CreateAuthorized(match);
    }

    public void Delete(int index) => Unavailable();

    public void DeleteByDBID(int databaseId)
    {
        var relays = GetRelays();
        if (_deleteById is null)
        {
            Unavailable();
            return;
        }

        try
        {
            _deleteById(databaseId);
            Volatile.Write(
                ref _relays,
                relays.Where(relay => relay.Id != databaseId).ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the incoming relay from the database.",
                EFail);
        }
    }

    public void Refresh()
    {
        _ = GetRelays();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var relays = _reload();
            ArgumentNullException.ThrowIfNull(relays);
            Volatile.Write(ref _relays, relays.ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of incoming relays from the database.",
                EFail);
        }
    }

    public IInterfaceIncomingRelay Add() => Unavailable<IInterfaceIncomingRelay>();

    private IReadOnlyList<IncomingRelayAdministrationSnapshot> GetRelays()
    {
        return Volatile.Read(ref _relays)
            ?? throw new COMException(
                "IncomingRelays access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private T Unavailable<T>()
    {
        _ = GetRelays();
        throw new COMException(
            "This IncomingRelays member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetRelays();
        throw new COMException(
            "This IncomingRelays member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("CB3F5F58-436C-4358-8E1C-1BE1F6D822BC")]
[ProgId("hMailServer.IncomingRelay.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceIncomingRelay))]
public sealed class IncomingRelay : IInterfaceIncomingRelay
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IncomingRelayAdministrationSnapshot? _relay;

    public IncomingRelay()
    {
    }

    private IncomingRelay(IncomingRelayAdministrationSnapshot relay)
    {
        _relay = relay;
    }

    public int ID => Snapshot.Id;

    public string LowerIP { get => Snapshot.LowerIp; set => Unavailable(); }

    public string UpperIP { get => Snapshot.UpperIp; set => Unavailable(); }

    public string Name { get => Snapshot.Name; set => Unavailable(); }

    internal static IncomingRelay CreateAuthorized(IncomingRelayAdministrationSnapshot relay) => new(relay);

    public void Delete() => Unavailable();

    public void Save() => Unavailable();

    private IncomingRelayAdministrationSnapshot Snapshot =>
        _relay ?? throw new COMException(
            "IncomingRelay access requires an authenticated server administrator.",
            EAccessDenied);

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This IncomingRelay member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class IncomingRelayAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IIncomingRelayAdministrationStore? _store;

    public static void Configure(IIncomingRelayAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static IncomingRelays CreateAuthorizedAdapter()
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer incoming relay administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<IncomingRelayAdministrationSnapshot> LoadRelays() => store
            .GetIncomingRelaysAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        void DeleteRelayById(int databaseId) => store
            .DeleteIncomingRelayByIdAsync(databaseId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return IncomingRelays.CreateAuthorized(LoadRelays(), LoadRelays, DeleteRelayById);
    }
}
