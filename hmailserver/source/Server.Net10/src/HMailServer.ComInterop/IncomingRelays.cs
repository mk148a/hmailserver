using System.Net;
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
    private readonly Action<IncomingRelayAdministrationSnapshot>? _save;
    private readonly Func<IncomingRelayAdministrationSnapshot, int>? _insert;
    private readonly Func<bool>? _isServerAdministrator;
    private readonly Func<CancellationToken, ValueTask<IDisposable?>>? _authorizationLeaseFactory;

    public IncomingRelays()
    {
    }

    private IncomingRelays(
        IReadOnlyList<IncomingRelayAdministrationSnapshot> relays,
        Func<IReadOnlyList<IncomingRelayAdministrationSnapshot>>? reload,
        Action<int>? deleteById,
        Action<IncomingRelayAdministrationSnapshot>? save,
        Func<IncomingRelayAdministrationSnapshot, int>? insert,
        Func<bool>? isServerAdministrator,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory)
    {
        _relays = relays.ToArray();
        _reload = reload;
        _deleteById = deleteById;
        _save = save;
        _insert = insert;
        _isServerAdministrator = isServerAdministrator;
        _authorizationLeaseFactory = authorizationLeaseFactory;
    }

    public int Count => GetRelays().Count;

    internal static IncomingRelays CreateAuthorized(
        IReadOnlyList<IncomingRelayAdministrationSnapshot> relays,
        Func<IReadOnlyList<IncomingRelayAdministrationSnapshot>>? reload = null,
        Action<int>? deleteById = null,
        Action<IncomingRelayAdministrationSnapshot>? save = null,
        Func<IncomingRelayAdministrationSnapshot, int>? insert = null,
        Func<bool>? isServerAdministrator = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
    {
        ArgumentNullException.ThrowIfNull(relays);
        return new IncomingRelays(
            relays,
            reload,
            deleteById,
            save,
            insert,
            isServerAdministrator,
            authorizationLeaseFactory);
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

            return CreateRelay(relays[index]);
        }
    }

    public IInterfaceIncomingRelay get_ItemByDBID(int databaseId)
    {
        var match = GetRelays().FirstOrDefault(relay => relay.Id == databaseId);

        return match is null
            ? throw new COMException("No incoming relay with the specified database identifier exists.", DispEBadIndex)
            : CreateRelay(match);
    }

    public IInterfaceIncomingRelay get_ItemByName(string itemName)
    {
        var match = GetRelays().FirstOrDefault(
            relay => string.Equals(relay.Name, itemName, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No incoming relay with the specified name exists.", DispEBadIndex)
            : CreateRelay(match);
    }

    public void Delete(int index)
    {
        var relays = GetRelays();
        if (index < 0 || index >= relays.Count)
        {
            throw new COMException("Incoming relay index was outside the collection.", DispEBadIndex);
        }

        if (_deleteById is null)
        {
            Unavailable();
            return;
        }

        EnsureServerAdministrator();
        var databaseId = relays[index].Id;
        using var authorizationLease = AcquireAuthorizationLease();
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

    public void DeleteByDBID(int databaseId)
    {
        var relays = GetRelays();
        if (_deleteById is null)
        {
            Unavailable();
            return;
        }

        if (!relays.Any(relay => relay.Id == databaseId))
        {
            return;
        }

        EnsureServerAdministrator();
        using var authorizationLease = AcquireAuthorizationLease();
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

    public IInterfaceIncomingRelay Add()
    {
        _ = GetRelays();
        if (_insert is null)
        {
            return Unavailable<IInterfaceIncomingRelay>();
        }

        return IncomingRelay.CreateAuthorized(
            new IncomingRelayAdministrationSnapshot(0, string.Empty, "0.0.0.0", "0.0.0.0"),
            save: SaveRelay,
            isServerAdministrator: _isServerAdministrator);
    }

    private IncomingRelayAdministrationSnapshot SaveRelay(IncomingRelayAdministrationSnapshot relay)
    {
        var relays = GetRelays();
        if (relay.Id == 0 && _insert is null)
        {
            Unavailable();
            return relay;
        }

        if (relay.Id != 0 && _save is null)
        {
            Unavailable();
            return relay;
        }

        using var authorizationLease = AcquireAuthorizationLease();
        try
        {
            if (relay.Id == 0)
            {
                var insertedRelay = relay with { Id = _insert!(relay) };
                Volatile.Write(ref _relays, relays.Concat([insertedRelay]).ToArray());
                return insertedRelay;
            }

            _save!(relay);
            Volatile.Write(
                ref _relays,
                relays
                    .Select(existing => existing.Id == relay.Id ? relay : existing)
                    .ToArray());
            return relay;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the incoming relay to the database.",
                EFail);
        }
    }

    private IReadOnlyList<IncomingRelayAdministrationSnapshot> GetRelays()
    {
        return Volatile.Read(ref _relays)
            ?? throw new COMException(
                "IncomingRelays access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "IncomingRelays access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private IDisposable? AcquireAuthorizationLease()
    {
        if (_authorizationLeaseFactory is null)
        {
            return null;
        }

        return _authorizationLeaseFactory(CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            ?? throw new COMException(
                "IncomingRelays access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private IncomingRelay CreateRelay(IncomingRelayAdministrationSnapshot relay)
    {
        return IncomingRelay.CreateAuthorized(
            relay,
            save: _save is null ? null : SaveRelay,
            delete: _deleteById is null ? null : DeleteByDBID,
            isServerAdministrator: _isServerAdministrator);
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

    private IncomingRelayAdministrationSnapshot? _relay;
    private readonly Func<IncomingRelayAdministrationSnapshot, IncomingRelayAdministrationSnapshot>? _save;
    private readonly Action<int>? _delete;
    private readonly Func<bool>? _isServerAdministrator;

    public IncomingRelay()
    {
    }

    private IncomingRelay(
        IncomingRelayAdministrationSnapshot relay,
        Func<IncomingRelayAdministrationSnapshot, IncomingRelayAdministrationSnapshot>? save,
        Action<int>? delete,
        Func<bool>? isServerAdministrator)
    {
        _relay = relay;
        _save = save;
        _delete = delete;
        _isServerAdministrator = isServerAdministrator;
    }

    public int ID => Snapshot.Id;

    public string LowerIP { get => Snapshot.LowerIp; set => Mutate(snapshot => snapshot with { LowerIp = NormalizeLegacyAddress(value) }); }

    public string UpperIP { get => Snapshot.UpperIp; set => Mutate(snapshot => snapshot with { UpperIp = NormalizeLegacyAddress(value) }); }

    public string Name { get => Snapshot.Name; set => Mutate(snapshot => snapshot with { Name = value ?? string.Empty }); }

    internal static IncomingRelay CreateAuthorized(
        IncomingRelayAdministrationSnapshot relay,
        Func<IncomingRelayAdministrationSnapshot, IncomingRelayAdministrationSnapshot>? save = null,
        Action<int>? delete = null,
        Func<bool>? isServerAdministrator = null) =>
        new(relay, save, delete, isServerAdministrator);

    public void Delete()
    {
        EnsureServerAdministrator();
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        _delete(Snapshot.Id);
    }

    public void Save()
    {
        EnsureServerAdministrator();
        if (_save is null)
        {
            Unavailable();
            return;
        }

        _relay = _save(Snapshot);
    }

    private IncomingRelayAdministrationSnapshot Snapshot =>
        _relay ?? throw new COMException(
            "IncomingRelay access requires an authenticated server administrator.",
            EAccessDenied);

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "IncomingRelay access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private void Mutate(Func<IncomingRelayAdministrationSnapshot, IncomingRelayAdministrationSnapshot> mutation)
    {
        if (_save is null)
        {
            Unavailable();
            return;
        }

        _relay = mutation(Snapshot);
    }

    private static string NormalizeLegacyAddress(string? value)
    {
        var address = value ?? string.Empty;
        if (address.Contains(':', StringComparison.Ordinal))
        {
            return IPAddress.TryParse(address, out var parsed) && parsed.GetAddressBytes().Length == 16
                ? parsed.ToString()
                : "::";
        }

        return TryParseLegacyIpv4(address, out var normalized)
            ? normalized
            : "0.0.0.0";
    }

    private static bool TryParseLegacyIpv4(string address, out string normalized)
    {
        var parts = address.Split('.');
        if (parts.Length != 4)
        {
            normalized = string.Empty;
            return false;
        }

        var bytes = new byte[4];
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            if (part.Length == 0 || part.Any(static value => value < '0' || value > '9'))
            {
                normalized = string.Empty;
                return false;
            }

            if (!int.TryParse(part, out var octet) || octet > 255)
            {
                normalized = string.Empty;
                return false;
            }

            bytes[index] = (byte)octet;
        }

        normalized = new IPAddress(bytes).ToString();
        return true;
    }

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

    internal static IncomingRelays CreateAuthorizedAdapter(
        Func<bool>? isServerAdministrator = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
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

        void SaveRelay(IncomingRelayAdministrationSnapshot relay) => store
            .UpdateIncomingRelayAsync(relay, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        int InsertRelay(IncomingRelayAdministrationSnapshot relay) => store
            .InsertIncomingRelayAsync(relay, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return IncomingRelays.CreateAuthorized(
            LoadRelays(),
            LoadRelays,
            DeleteRelayById,
            SaveRelay,
            InsertRelay,
            isServerAdministrator,
            authorizationLeaseFactory);
    }
}
