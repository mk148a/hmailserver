using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("E4100C8D-E956-449C-A96D-261DDC33AE4F")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceDomainAliases
{
    [DispId(0)]
    IInterfaceDomainAlias this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    [SpecialName]
    IInterfaceDomainAlias get_ItemByDBID(int databaseId);

    [DispId(3)]
    void Refresh();

    [DispId(4)]
    void Delete(int index);

    [DispId(5)]
    void DeleteByDBID(int databaseId);

    [DispId(6)]
    IInterfaceDomainAlias Add();
}

[ComVisible(true)]
[Guid("8FD251D8-AAF1-4143-B185-E6C1BF281826")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceDomainAlias
{
    [DispId(1)]
    int ID { get; }

    [DispId(2)]
    string AliasName
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        get;

        [param: MarshalAs(UnmanagedType.BStr)]
        set;
    }

    [DispId(3)]
    int DomainID { get; set; }

    [DispId(4)]
    void Save();

    [DispId(5)]
    void Delete();
}

[ComVisible(true)]
[Guid("DC25B3AD-0360-49CA-AD4B-06FA42B9DF04")]
[ProgId("hMailServer.DomainAliases.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDomainAliases))]
public sealed class DomainAliases : IInterfaceDomainAliases
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private DomainAliasAdministrationSnapshot[]? _aliases;
    private readonly Func<IReadOnlyList<DomainAliasAdministrationSnapshot>>? _reload;
    private readonly Func<int, DomainAliasAdministrationSnapshot, int>? _insert;
    private readonly Action<int, DomainAliasAdministrationSnapshot>? _update;
    private readonly Func<int, int, bool>? _delete;
    private readonly int? _owningDomainId;
    private readonly Func<bool>? _isAuthenticated;
    private readonly Func<CancellationToken, ValueTask<IDisposable?>>? _authorizationLeaseFactory;

    public DomainAliases()
    {
    }

    private DomainAliases(
        IReadOnlyList<DomainAliasAdministrationSnapshot> aliases,
        Func<IReadOnlyList<DomainAliasAdministrationSnapshot>>? reload,
        Func<int, DomainAliasAdministrationSnapshot, int>? insert,
        Action<int, DomainAliasAdministrationSnapshot>? update,
        Func<int, int, bool>? delete,
        int? owningDomainId,
        Func<bool>? isAuthenticated,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory)
    {
        _aliases = aliases.ToArray();
        _reload = reload;
        _insert = insert;
        _update = update;
        _delete = delete;
        _owningDomainId = owningDomainId;
        _isAuthenticated = isAuthenticated;
        _authorizationLeaseFactory = authorizationLeaseFactory;
    }

    public int Count => GetAliases().Count;

    internal static DomainAliases CreateAuthorized(
        IReadOnlyList<DomainAliasAdministrationSnapshot> aliases,
        Func<IReadOnlyList<DomainAliasAdministrationSnapshot>>? reload = null,
        Func<int, DomainAliasAdministrationSnapshot, int>? insert = null,
        Action<int, DomainAliasAdministrationSnapshot>? update = null,
        Func<int, int, bool>? delete = null,
        int? owningDomainId = null,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        return new DomainAliases(
            aliases,
            reload,
            insert,
            update,
            delete,
            owningDomainId,
            isAuthenticated,
            authorizationLeaseFactory);
    }

    public IInterfaceDomainAlias this[int index]
    {
        get
        {
            var aliases = GetAliases();
            if (index < 0 || index >= aliases.Count)
            {
                throw new COMException("Domain alias index was outside the collection.", DispEBadIndex);
            }

            return CreateExistingAlias(aliases[index]);
        }
    }

    public IInterfaceDomainAlias get_ItemByDBID(int databaseId)
    {
        var match = GetAliases().FirstOrDefault(alias => alias.Id == databaseId);

        return match is null
            ? throw new COMException("No domain alias with the specified database identifier exists.", DispEBadIndex)
            : CreateExistingAlias(match);
    }

    public void Refresh()
    {
        _ = GetAliases();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var aliases = _reload();
            ArgumentNullException.ThrowIfNull(aliases);
            Volatile.Write(ref _aliases, aliases.ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of domain aliases from the database.",
                EFail);
        }
    }

    public void Delete(int index)
    {
        var aliases = GetAliases();
        if (_delete is null || _owningDomainId is null)
        {
            Unavailable();
        }

        if (index < 0 || index >= aliases.Count)
        {
            return;
        }

        DeleteExistingDomainAlias(aliases[index].Id, acquireAuthorizationLease: true);
    }

    public void DeleteByDBID(int databaseId)
    {
        var aliases = GetAliases();
        if (_delete is null || _owningDomainId is null)
        {
            Unavailable();
        }

        if (!aliases.Any(alias => alias.Id == databaseId))
        {
            return;
        }

        DeleteExistingDomainAlias(databaseId, acquireAuthorizationLease: true);
    }

    public IInterfaceDomainAlias Add()
    {
        _ = GetAliases();
        if (_insert is null || _owningDomainId is null)
        {
            return Unavailable<IInterfaceDomainAlias>();
        }

        var entry = new DomainAliasAdministrationEntry(
            new DomainAliasAdministrationSnapshot(0, _owningDomainId.Value, string.Empty));
        return DomainAlias.CreateAuthorized(
            entry,
            save: _update is null ? null : alias => SaveExistingDomainAlias(entry, alias, authorizationLeaseAlreadyHeld: true),
            saveNew: alias => SaveNewDomainAlias(entry, alias, authorizationLeaseAlreadyHeld: true),
            isAuthenticated: _isAuthenticated,
            authorizationLeaseFactory: _authorizationLeaseFactory);
    }

    private IInterfaceDomainAlias CreateExistingAlias(DomainAliasAdministrationSnapshot alias)
    {
        var entry = new DomainAliasAdministrationEntry(alias);
        return DomainAlias.CreateAuthorized(
            entry,
            save: _update is null ? null : snapshot => SaveExistingDomainAlias(entry, snapshot, authorizationLeaseAlreadyHeld: true),
            delete: _delete is null ? null : aliasId => DeleteExistingDomainAlias(aliasId, acquireAuthorizationLease: false),
            isAuthenticated: _isAuthenticated,
            authorizationLeaseFactory: _authorizationLeaseFactory);
    }

    private IReadOnlyList<DomainAliasAdministrationSnapshot> GetAliases()
    {
        EnsureAuthenticated();
        return Volatile.Read(ref _aliases)
            ?? throw new COMException(
                "DomainAliases access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private DomainAliasAdministrationSnapshot SaveNewDomainAlias(
        DomainAliasAdministrationEntry entry,
        DomainAliasAdministrationSnapshot alias,
        bool authorizationLeaseAlreadyHeld)
    {
        var aliases = authorizationLeaseAlreadyHeld
            ? GetAliasesWithoutAuthentication()
            : GetAliases();
        if (_insert is null || _owningDomainId is null)
        {
            Unavailable();
        }

        var prepared = alias with
        {
            Id = 0,
            DomainId = _owningDomainId.GetValueOrDefault()
        };

        try
        {
            var generatedId = _insert!(_owningDomainId.GetValueOrDefault(), prepared);
            if (generatedId <= 0)
            {
                throw new InvalidOperationException(
                    "The domain alias insert did not return a valid generated identity.");
            }

            var persisted = prepared with { Id = generatedId };
            entry.Snapshot = persisted;
            Volatile.Write(ref _aliases, aliases.Append(persisted).ToArray());
            return persisted;
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the domain alias to the database.",
                EFail);
        }
    }

    private void EnsureAuthenticated()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "DomainAliases access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private void DeleteExistingDomainAlias(int aliasId, bool acquireAuthorizationLease)
    {
        var aliases = acquireAuthorizationLease
            ? GetAliases()
            : GetAliasesWithoutAuthentication();
        if (_delete is null || _owningDomainId is null)
        {
            Unavailable();
        }

        if (!aliases.Any(alias => alias.Id == aliasId))
        {
            return;
        }

        try
        {
            using var authorizationLease = acquireAuthorizationLease
                ? AcquireAuthorizationLease()
                : null;
            if (!_delete!(_owningDomainId.GetValueOrDefault(), aliasId))
            {
                throw new InvalidOperationException(
                    "The domain alias delete did not affect exactly one row.");
            }

            Volatile.Write(
                ref _aliases,
                aliases.Where(alias => alias.Id != aliasId).ToArray());
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the domain alias from the database.",
                EFail);
        }
    }

    private void SaveExistingDomainAlias(
        DomainAliasAdministrationEntry entry,
        DomainAliasAdministrationSnapshot alias,
        bool authorizationLeaseAlreadyHeld)
    {
        var aliases = authorizationLeaseAlreadyHeld
            ? GetAliasesWithoutAuthentication()
            : GetAliases();
        if (_update is null || _owningDomainId is null)
        {
            Unavailable();
        }

        if (!aliases.Any(existing => existing.Id == alias.Id))
        {
            return;
        }

        var prepared = alias with { DomainId = _owningDomainId.GetValueOrDefault() };
        try
        {
            _update!(_owningDomainId.GetValueOrDefault(), prepared);
            entry.Snapshot = prepared;
            Volatile.Write(
                ref _aliases,
                aliases.Select(existing => existing.Id == prepared.Id ? prepared : existing).ToArray());
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the domain alias to the database.",
                EFail);
        }
    }

    private T Unavailable<T>()
    {
        _ = GetAliases();
        throw new COMException(
            "This DomainAliases member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetAliases();
        throw new COMException(
            "This DomainAliases member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private IReadOnlyList<DomainAliasAdministrationSnapshot> GetAliasesWithoutAuthentication() =>
        Volatile.Read(ref _aliases)
            ?? throw new COMException(
                "DomainAliases access requires an authenticated server administrator.",
                EAccessDenied);

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
                "DomainAliases access requires an authenticated server administrator.",
                EAccessDenied);
    }
}

[ComVisible(false)]
internal sealed class DomainAliasAdministrationEntry
{
    internal DomainAliasAdministrationEntry(DomainAliasAdministrationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Snapshot = snapshot;
    }

    internal DomainAliasAdministrationSnapshot Snapshot { get; set; }
}

[ComVisible(true)]
[Guid("D0061C74-5588-4796-B564-FE5DE85495DC")]
[ProgId("hMailServer.DomainAlias.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDomainAlias))]
public sealed class DomainAlias : IInterfaceDomainAlias
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly DomainAliasAdministrationEntry? _entry;
    private readonly Action<DomainAliasAdministrationSnapshot>? _save;
    private readonly Func<DomainAliasAdministrationSnapshot, DomainAliasAdministrationSnapshot>? _saveNew;
    private readonly Action<int>? _delete;
    private readonly Func<bool>? _isAuthenticated;
    private readonly Func<CancellationToken, ValueTask<IDisposable?>>? _authorizationLeaseFactory;

    public DomainAlias()
    {
    }

    private DomainAlias(
        DomainAliasAdministrationEntry entry,
        Action<DomainAliasAdministrationSnapshot>? save,
        Func<DomainAliasAdministrationSnapshot, DomainAliasAdministrationSnapshot>? saveNew,
        Action<int>? delete,
        Func<bool>? isAuthenticated,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory)
    {
        _entry = entry;
        _save = save;
        _saveNew = saveNew;
        _delete = delete;
        _isAuthenticated = isAuthenticated;
        _authorizationLeaseFactory = authorizationLeaseFactory;
    }

    public int ID => Snapshot.Id;

    public string AliasName
    {
        get => Snapshot.AliasName;
        set => Mutate(snapshot => snapshot with { AliasName = value ?? string.Empty });
    }

    public int DomainID
    {
        get => Snapshot.DomainId;
        set => _ = Snapshot;
    }

    internal static DomainAlias CreateAuthorized(
        DomainAliasAdministrationSnapshot alias,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null) =>
        new(new DomainAliasAdministrationEntry(alias), null, null, null, isAuthenticated, authorizationLeaseFactory);

    internal static DomainAlias CreateAuthorized(
        DomainAliasAdministrationEntry entry,
        Action<DomainAliasAdministrationSnapshot>? save = null,
        Func<DomainAliasAdministrationSnapshot, DomainAliasAdministrationSnapshot>? saveNew = null,
        Action<int>? delete = null,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null) =>
        new(entry, save, saveNew, delete, isAuthenticated, authorizationLeaseFactory);

    public void Save()
    {
        EnsureAuthenticated();
        var snapshot = Snapshot;
        if (snapshot.Id == 0 && _saveNew is not null)
        {
            try
            {
                using var authorizationLease = AcquireAuthorizationLease();
                _entry!.Snapshot = _saveNew(snapshot);
            }
            catch (COMException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new COMException(
                    "It was not possible to save the domain alias to the database.",
                    EFail);
            }

            return;
        }

        if (snapshot.Id > 0 && _save is not null)
        {
            try
            {
                using var authorizationLease = AcquireAuthorizationLease();
                _save(snapshot);
            }
            catch (COMException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new COMException(
                    "It was not possible to save the domain alias to the database.",
                    EFail);
            }

            return;
        }

        if (Snapshot.Id == 0 || _save is null)
        {
            Unavailable();
            return;
        }
    }

    public void Delete()
    {
        EnsureAuthenticated();
        var snapshot = Snapshot;
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        try
        {
            using var authorizationLease = AcquireAuthorizationLease();
            _delete(snapshot.Id);
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the domain alias from the database.",
                EFail);
        }
    }

    private DomainAliasAdministrationSnapshot Snapshot =>
        EnsureAuthenticatedAndGetSnapshot();

    private void Mutate(Func<DomainAliasAdministrationSnapshot, DomainAliasAdministrationSnapshot> mutation)
    {
        EnsureAuthenticated();
        if (_save is null && _saveNew is null)
        {
            Unavailable();
            return;
        }

        _entry!.Snapshot = mutation(Snapshot);
    }

    private DomainAliasAdministrationSnapshot EnsureAuthenticatedAndGetSnapshot()
    {
        EnsureAuthenticated();
        return _entry?.Snapshot ?? throw new COMException(
            "DomainAlias access requires an authenticated server administrator.",
            EAccessDenied);
    }

    private void EnsureAuthenticated()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "DomainAlias access requires an authenticated server administrator.",
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
                "DomainAlias access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This DomainAlias member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class DomainAliasAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IDomainAliasAdministrationStore? _store;

    public static void Configure(IDomainAliasAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static DomainAliases CreateAuthorizedAdapter(
        int domainId,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer domain-alias administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<DomainAliasAdministrationSnapshot> LoadAliases() => store
            .GetDomainAliasesAsync(domainId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        int InsertDomainAlias(int owningDomainId, DomainAliasAdministrationSnapshot alias) => store
            .InsertDomainAliasAsync(owningDomainId, alias, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        void UpdateDomainAlias(int owningDomainId, DomainAliasAdministrationSnapshot alias) => store
            .UpdateDomainAliasAsync(owningDomainId, alias, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        bool DeleteDomainAlias(int owningDomainId, int aliasId) => store
            .DeleteDomainAliasAsync(owningDomainId, aliasId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return DomainAliases.CreateAuthorized(
            LoadAliases(),
            LoadAliases,
            InsertDomainAlias,
            UpdateDomainAlias,
            DeleteDomainAlias,
            domainId,
            isAuthenticated,
            authorizationLeaseFactory);
    }
}
