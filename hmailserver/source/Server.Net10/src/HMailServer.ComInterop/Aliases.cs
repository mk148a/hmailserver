using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("11AA2C23-66BA-4DE0-92AB-C4F8DCC21D32")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceAliases
{
    [DispId(0)]
    IInterfaceAlias this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    void Delete(int index);

    [DispId(3)]
    void Refresh();

    [DispId(4)]
    IInterfaceAlias Add();

    [DispId(5)]
    [SpecialName]
    IInterfaceAlias get_ItemByDBID(int databaseId);

    [DispId(6)]
    void DeleteByDBID(int databaseId);

    [DispId(7)]
    [SpecialName]
    IInterfaceAlias get_ItemByName([MarshalAs(UnmanagedType.BStr)] string name);
}

[ComVisible(true)]
[Guid("9420A3E9-ED5C-4699-98BE-0CBF3B7D3714")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceAlias
{
    [DispId(1)]
    bool Active
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(2)]
    int DomainID { get; set; }

    [DispId(3)]
    int ID { get; }

    [DispId(4)]
    string Name
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        get;

        [param: MarshalAs(UnmanagedType.BStr)]
        set;
    }

    [DispId(6)]
    string Value
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        get;

        [param: MarshalAs(UnmanagedType.BStr)]
        set;
    }

    [DispId(7)]
    void Delete();

    [DispId(8)]
    void Save();
}

[ComVisible(true)]
[Guid("1FE5E5F1-870A-4139-9EC1-DFFA3A9A58C8")]
[ProgId("hMailServer.Aliases.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceAliases))]
public sealed class Aliases : IInterfaceAliases
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private AliasAdministrationSnapshot[]? _aliases;
    private readonly Func<IReadOnlyList<AliasAdministrationSnapshot>>? _reload;
    private readonly Func<int, AliasAdministrationSnapshot, int>? _insert;
    private readonly Action<int, AliasAdministrationSnapshot>? _save;
    private readonly Func<int, int, bool>? _delete;
    private readonly int? _owningDomainId;
    private readonly Func<bool>? _isAuthenticated;

    public Aliases()
    {
    }

    private Aliases(
        IReadOnlyList<AliasAdministrationSnapshot> aliases,
        Func<IReadOnlyList<AliasAdministrationSnapshot>>? reload,
        Func<int, AliasAdministrationSnapshot, int>? insert,
        Action<int, AliasAdministrationSnapshot>? save,
        Func<int, int, bool>? delete,
        int? owningDomainId,
        Func<bool>? isAuthenticated)
    {
        _aliases = aliases.ToArray();
        _reload = reload;
        _insert = insert;
        _save = save;
        _delete = delete;
        _owningDomainId = owningDomainId;
        _isAuthenticated = isAuthenticated;
    }

    public int Count => GetAliases().Count;

    internal static Aliases CreateAuthorized(
        IReadOnlyList<AliasAdministrationSnapshot> aliases,
        Func<IReadOnlyList<AliasAdministrationSnapshot>>? reload = null,
        Func<int, AliasAdministrationSnapshot, int>? insert = null,
        Action<int, AliasAdministrationSnapshot>? save = null,
        Func<int, int, bool>? delete = null,
        int? owningDomainId = null,
        Func<bool>? isAuthenticated = null)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        return new Aliases(aliases, reload, insert, save, delete, owningDomainId, isAuthenticated);
    }

    public IInterfaceAlias this[int index]
    {
        get
        {
            var aliases = GetAliases();
            if (index < 0 || index >= aliases.Count)
            {
                throw new COMException("Alias index was outside the collection.", DispEBadIndex);
            }

            var entry = new AliasAdministrationEntry(aliases[index]);
            return Alias.CreateAuthorized(
                entry,
                save: _save is null ? null : alias => SaveExistingAlias(entry, alias),
                delete: _delete is null ? null : DeleteExistingAlias,
                isAuthenticated: _isAuthenticated);
        }
    }

    public void Delete(int index) => Unavailable();

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
                "It was not possible to retrieve a list of aliases from the database.",
                EFail);
        }
    }

    public IInterfaceAlias Add()
    {
        _ = GetAliases();
        if (_insert is null || _owningDomainId is null)
        {
            return Unavailable<IInterfaceAlias>();
        }

        var entry = new AliasAdministrationEntry(
            new AliasAdministrationSnapshot(
                Id: 0,
                DomainId: _owningDomainId.Value,
                Name: string.Empty,
                Value: string.Empty,
                Active: false));

        return Alias.CreateAuthorized(
            entry,
            saveNew: snapshot => SaveNewAlias(entry, snapshot),
            isAuthenticated: _isAuthenticated);
    }

    public IInterfaceAlias get_ItemByDBID(int databaseId)
    {
        var match = GetAliases().FirstOrDefault(alias => alias.Id == databaseId);

        return match is null
            ? throw new COMException("No alias with the specified database identifier exists.", DispEBadIndex)
            : CreateExistingAlias(match);
    }

    public void DeleteByDBID(int databaseId)
    {
        _ = GetAliases();
        if (_delete is null || _owningDomainId is null)
        {
            Unavailable();
        }

        if (!GetAliases().Any(alias => alias.Id == databaseId))
        {
            return;
        }

        DeleteExistingAlias(databaseId);
    }

    public IInterfaceAlias get_ItemByName(string name)
    {
        var match = GetAliases()
            .FirstOrDefault(alias => alias.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No alias with the specified name exists.", DispEBadIndex)
            : CreateExistingAlias(match);
    }

    private IReadOnlyList<AliasAdministrationSnapshot> GetAliases()
    {
        EnsureAuthenticated();
        return Volatile.Read(ref _aliases)
            ?? throw new COMException("Aliases access requires an authenticated server administrator.", EAccessDenied);
    }

    private AliasAdministrationSnapshot SaveNewAlias(
        AliasAdministrationEntry entry,
        AliasAdministrationSnapshot alias)
    {
        EnsureAuthenticated();
        var aliases = GetAliases();
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
                throw new InvalidOperationException("The alias insert did not return a valid generated identity.");
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
                "It was not possible to save the alias to the database.",
                EFail);
        }
    }

    private IInterfaceAlias CreateExistingAlias(AliasAdministrationSnapshot alias)
    {
        var entry = new AliasAdministrationEntry(alias);
        return Alias.CreateAuthorized(
            entry,
            save: _save is null ? null : snapshot => SaveExistingAlias(entry, snapshot),
            delete: _delete is null ? null : DeleteExistingAlias,
            isAuthenticated: _isAuthenticated);
    }

    private void SaveExistingAlias(
        AliasAdministrationEntry entry,
        AliasAdministrationSnapshot alias)
    {
        EnsureAuthenticated();
        var aliases = GetAliases();
        if (_save is null || _owningDomainId is null)
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
            _save!(_owningDomainId.GetValueOrDefault(), prepared);
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
                "It was not possible to save the alias to the database.",
                EFail);
        }
    }

    private void DeleteExistingAlias(int aliasId)
    {
        EnsureAuthenticated();
        var aliases = GetAliases();
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
            if (!_delete!(_owningDomainId.GetValueOrDefault(), aliasId))
            {
                throw new InvalidOperationException(
                    $"Deleting alias {aliasId} for owning domain {_owningDomainId.GetValueOrDefault()} did not affect one row.");
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
                "It was not possible to delete the alias from the database.",
                EFail);
        }
    }

    private void EnsureAuthenticated()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "Aliases access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private T Unavailable<T>()
    {
        _ = GetAliases();
        throw new COMException("This Aliases member is not implemented by the .NET 10 rewrite yet.", ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetAliases();
        throw new COMException("This Aliases member is not implemented by the .NET 10 rewrite yet.", ENotImplemented);
    }
}

[ComVisible(false)]
internal sealed class AliasAdministrationEntry
{
    internal AliasAdministrationEntry(AliasAdministrationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Snapshot = snapshot;
    }

    internal AliasAdministrationSnapshot Snapshot { get; set; }
}

[ComVisible(true)]
[Guid("335CE9E1-32C5-4CB0-8BF6-CB925196E4D6")]
[ProgId("hMailServer.Alias.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceAlias))]
public sealed class Alias : IInterfaceAlias
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly AliasAdministrationEntry? _entry;
    private readonly Action<AliasAdministrationSnapshot>? _save;
    private readonly Func<AliasAdministrationSnapshot, AliasAdministrationSnapshot>? _saveNew;
    private readonly Action<int>? _delete;
    private readonly Func<bool>? _isAuthenticated;

    public Alias()
    {
    }

    private Alias(
        AliasAdministrationEntry entry,
        Action<AliasAdministrationSnapshot>? save,
        Func<AliasAdministrationSnapshot, AliasAdministrationSnapshot>? saveNew,
        Action<int>? delete,
        Func<bool>? isAuthenticated)
    {
        _entry = entry;
        _save = save;
        _saveNew = saveNew;
        _delete = delete;
        _isAuthenticated = isAuthenticated;
    }

    public bool Active
    {
        get => Snapshot.Active;
        set => Mutate(snapshot => snapshot with { Active = value });
    }

    public int DomainID
    {
        get => Snapshot.DomainId;
        set => _ = Snapshot;
    }

    public int ID => Snapshot.Id;

    public string Name
    {
        get => Snapshot.Name;
        set => Mutate(snapshot => snapshot with { Name = value });
    }

    public string Value
    {
        get => Snapshot.Value;
        set => Mutate(snapshot => snapshot with { Value = value });
    }

    internal static Alias CreateAuthorized(
        AliasAdministrationSnapshot alias,
        Func<bool>? isAuthenticated = null) =>
        new(new AliasAdministrationEntry(alias), null, null, null, isAuthenticated);

    internal static Alias CreateAuthorized(
        AliasAdministrationEntry entry,
        Action<AliasAdministrationSnapshot>? save = null,
        Func<AliasAdministrationSnapshot, AliasAdministrationSnapshot>? saveNew = null,
        Action<int>? delete = null,
        Func<bool>? isAuthenticated = null) =>
        new(entry, save, saveNew, delete, isAuthenticated);

    public void Delete()
    {
        EnsureAuthenticated();
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        try
        {
            _delete(Snapshot.Id);
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the alias from the database.",
                EFail);
        }
    }

    public void Save()
    {
        EnsureAuthenticated();
        var snapshot = Snapshot;
        if (snapshot.Id == 0 && _saveNew is not null)
        {
            try
            {
                _entry!.Snapshot = _saveNew(snapshot);
            }
            catch (COMException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new COMException(
                    "It was not possible to save the alias to the database.",
                    EFail);
            }

            return;
        }

        if (snapshot.Id > 0 && _save is not null)
        {
            try
            {
                _save(snapshot);
            }
            catch (COMException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new COMException(
                    "It was not possible to save the alias to the database.",
                    EFail);
            }

            return;
        }

        Unavailable();
    }

    private void Mutate(Func<AliasAdministrationSnapshot, AliasAdministrationSnapshot> mutation)
    {
        EnsureAuthenticated();
        if (_save is null && _saveNew is null)
        {
            Unavailable();
            return;
        }

        _entry!.Snapshot = mutation(Snapshot);
    }

    private void EnsureAuthenticated()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "Alias access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private AliasAdministrationSnapshot Snapshot
    {
        get
        {
            EnsureAuthenticated();
            return _entry?.Snapshot
                ?? throw new COMException("Alias access requires an authenticated server administrator.", EAccessDenied);
        }
    }

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException("This Alias member is not implemented by the .NET 10 rewrite yet.", ENotImplemented);
    }
}

[ComVisible(false)]
public static class AliasAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IAliasAdministrationStore? _store;

    public static void Configure(IAliasAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static Aliases CreateAuthorizedAdapter(
        int domainId,
        Func<bool>? isAuthenticated = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer alias administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<AliasAdministrationSnapshot> LoadAliases() => store
            .GetAliasesAsync(domainId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        int InsertAlias(int owningDomainId, AliasAdministrationSnapshot alias) => store
            .InsertAliasAsync(owningDomainId, alias, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        void SaveAlias(int owningDomainId, AliasAdministrationSnapshot alias) => store
            .UpdateAliasAsync(owningDomainId, alias, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        bool DeleteAlias(int owningDomainId, int aliasId) => store
            .DeleteAliasAsync(owningDomainId, aliasId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return Aliases.CreateAuthorized(
            LoadAliases(),
            LoadAliases,
            InsertAlias,
            SaveAlias,
            DeleteAlias,
            domainId,
            isAuthenticated);
    }
}
