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
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<AliasAdministrationSnapshot>? _aliases;

    public Aliases()
    {
    }

    private Aliases(IReadOnlyList<AliasAdministrationSnapshot> aliases)
    {
        _aliases = aliases.ToArray();
    }

    public int Count => GetAliases().Count;

    internal static Aliases CreateAuthorized(IReadOnlyList<AliasAdministrationSnapshot> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        return new Aliases(aliases);
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

            return Alias.CreateAuthorized(aliases[index]);
        }
    }

    public void Delete(int index) => Unavailable();

    public void Refresh() => Unavailable();

    public IInterfaceAlias Add() => Unavailable<IInterfaceAlias>();

    public IInterfaceAlias get_ItemByDBID(int databaseId)
    {
        var match = GetAliases().FirstOrDefault(alias => alias.Id == databaseId);

        return match is null
            ? throw new COMException("No alias with the specified database identifier exists.", DispEBadIndex)
            : Alias.CreateAuthorized(match);
    }

    public void DeleteByDBID(int databaseId) => Unavailable();

    public IInterfaceAlias get_ItemByName(string name)
    {
        var match = GetAliases()
            .FirstOrDefault(alias => alias.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No alias with the specified name exists.", DispEBadIndex)
            : Alias.CreateAuthorized(match);
    }

    private IReadOnlyList<AliasAdministrationSnapshot> GetAliases()
    {
        return _aliases
            ?? throw new COMException("Aliases access requires an authenticated server administrator.", EAccessDenied);
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

[ComVisible(true)]
[Guid("335CE9E1-32C5-4CB0-8BF6-CB925196E4D6")]
[ProgId("hMailServer.Alias.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceAlias))]
public sealed class Alias : IInterfaceAlias
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly AliasAdministrationSnapshot? _alias;

    public Alias()
    {
    }

    private Alias(AliasAdministrationSnapshot alias)
    {
        _alias = alias;
    }

    public bool Active
    {
        get => Snapshot.Active;
        set => Unavailable();
    }

    public int DomainID
    {
        get => Snapshot.DomainId;
        set => Unavailable();
    }

    public int ID => Snapshot.Id;

    public string Name
    {
        get => Snapshot.Name;
        set => Unavailable();
    }

    public string Value
    {
        get => Snapshot.Value;
        set => Unavailable();
    }

    internal static Alias CreateAuthorized(AliasAdministrationSnapshot alias) => new(alias);

    public void Delete() => Unavailable();

    public void Save() => Unavailable();

    private AliasAdministrationSnapshot Snapshot =>
        _alias ?? throw new COMException("Alias access requires an authenticated server administrator.", EAccessDenied);

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

    internal static Aliases CreateAuthorizedAdapter(int domainId)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer alias administration runtime has not been initialized.",
                CoENotInitialized);

        var aliases = store
            .GetAliasesAsync(domainId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return Aliases.CreateAuthorized(aliases);
    }
}
