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

    public DomainAliases()
    {
    }

    private DomainAliases(
        IReadOnlyList<DomainAliasAdministrationSnapshot> aliases,
        Func<IReadOnlyList<DomainAliasAdministrationSnapshot>>? reload)
    {
        _aliases = aliases.ToArray();
        _reload = reload;
    }

    public int Count => GetAliases().Count;

    internal static DomainAliases CreateAuthorized(
        IReadOnlyList<DomainAliasAdministrationSnapshot> aliases,
        Func<IReadOnlyList<DomainAliasAdministrationSnapshot>>? reload = null)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        return new DomainAliases(aliases, reload);
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

            return DomainAlias.CreateAuthorized(aliases[index]);
        }
    }

    public IInterfaceDomainAlias get_ItemByDBID(int databaseId)
    {
        var match = GetAliases().FirstOrDefault(alias => alias.Id == databaseId);

        return match is null
            ? throw new COMException("No domain alias with the specified database identifier exists.", DispEBadIndex)
            : DomainAlias.CreateAuthorized(match);
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

    public void Delete(int index) => Unavailable();

    public void DeleteByDBID(int databaseId) => Unavailable();

    public IInterfaceDomainAlias Add() => Unavailable<IInterfaceDomainAlias>();

    private IReadOnlyList<DomainAliasAdministrationSnapshot> GetAliases()
    {
        return Volatile.Read(ref _aliases)
            ?? throw new COMException(
                "DomainAliases access requires an authenticated server administrator.",
                EAccessDenied);
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
}

[ComVisible(true)]
[Guid("D0061C74-5588-4796-B564-FE5DE85495DC")]
[ProgId("hMailServer.DomainAlias.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDomainAlias))]
public sealed class DomainAlias : IInterfaceDomainAlias
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly DomainAliasAdministrationSnapshot? _alias;

    public DomainAlias()
    {
    }

    private DomainAlias(DomainAliasAdministrationSnapshot alias)
    {
        _alias = alias;
    }

    public int ID => Snapshot.Id;

    public string AliasName
    {
        get => Snapshot.AliasName;
        set => Unavailable();
    }

    public int DomainID
    {
        get => Snapshot.DomainId;
        set => Unavailable();
    }

    internal static DomainAlias CreateAuthorized(DomainAliasAdministrationSnapshot alias) => new(alias);

    public void Save() => Unavailable();

    public void Delete() => Unavailable();

    private DomainAliasAdministrationSnapshot Snapshot =>
        _alias ?? throw new COMException(
            "DomainAlias access requires an authenticated server administrator.",
            EAccessDenied);

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

    internal static DomainAliases CreateAuthorizedAdapter(int domainId)
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

        return DomainAliases.CreateAuthorized(LoadAliases(), LoadAliases);
    }
}
