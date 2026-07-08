using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("111F318A-C087-4091-BD1F-4226230EE513")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceRoutes
{
    [DispId(0)]
    IInterfaceRoute this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    void DeleteByDBID(int databaseId);

    [DispId(3)]
    IInterfaceRoute Add();

    [DispId(4)]
    [SpecialName]
    IInterfaceRoute get_ItemByName([MarshalAs(UnmanagedType.BStr)] string itemName);

    [DispId(5)]
    [SpecialName]
    IInterfaceRoute get_ItemByDBID(int databaseId);

    [DispId(6)]
    void Refresh();
}

[ComVisible(true)]
[Guid("F78FA851-D3D3-4A28-AFCC-A471C00781D3")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceRoute
{
    [DispId(1)]
    int ID { get; }

    [DispId(2)]
    string DomainName { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(3)]
    string TargetSMTPHost { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(4)]
    int TargetSMTPPort { get; set; }

    [DispId(5)]
    int NumberOfTries { get; set; }

    [DispId(6)]
    int MinutesBetweenTry { get; set; }

    [DispId(7)]
    bool AllAddresses
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(8)]
    IInterfaceRouteAddresses Addresses { get; }

    [DispId(9)]
    bool RelayerRequiresAuth
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(10)]
    string RelayerAuthUsername { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(11)]
    void SetRelayerAuthPassword([MarshalAs(UnmanagedType.BStr)] string newValue);

    [DispId(12)]
    bool TreatSecurityAsLocalDomain
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(13)]
    void Save();

    [DispId(14)]
    bool UseSSL
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(15)]
    string Description { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(16)]
    void Delete();

    [DispId(17)]
    bool TreatSenderAsLocalDomain
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(18)]
    bool TreatRecipientAsLocalDomain
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(19)]
    ComConnectionSecurity ConnectionSecurity { get; set; }
}

[ComVisible(true)]
[Guid("7D174A9D-D44C-4627-BE78-E5DDC513C31F")]
[ProgId("hMailServer.Routes.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceRoutes))]
public sealed class Routes : IInterfaceRoutes
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private RouteAdministrationSnapshot[]? _routes;
    private readonly Func<IReadOnlyList<RouteAdministrationSnapshot>>? _reload;

    public Routes()
    {
    }

    private Routes(
        IReadOnlyList<RouteAdministrationSnapshot> routes,
        Func<IReadOnlyList<RouteAdministrationSnapshot>>? reload)
    {
        _routes = routes.ToArray();
        _reload = reload;
    }

    public int Count => GetRoutes().Count;

    internal static Routes CreateAuthorized(
        IReadOnlyList<RouteAdministrationSnapshot> routes,
        Func<IReadOnlyList<RouteAdministrationSnapshot>>? reload = null)
    {
        ArgumentNullException.ThrowIfNull(routes);
        return new Routes(routes, reload);
    }

    public IInterfaceRoute this[int index]
    {
        get
        {
            var routes = GetRoutes();
            if (index < 0 || index >= routes.Count)
            {
                throw new COMException("Route index was outside the collection.", DispEBadIndex);
            }

            return Route.CreateAuthorized(routes[index]);
        }
    }

    public IInterfaceRoute get_ItemByName(string itemName)
    {
        var match = GetRoutes().FirstOrDefault(
            route => string.Equals(route.DomainName, itemName, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No route with the specified domain name exists.", DispEBadIndex)
            : Route.CreateAuthorized(match);
    }

    public IInterfaceRoute get_ItemByDBID(int databaseId)
    {
        var match = GetRoutes().FirstOrDefault(route => route.Id == databaseId);

        return match is null
            ? throw new COMException("No route with the specified database identifier exists.", DispEBadIndex)
            : Route.CreateAuthorized(match);
    }

    public void DeleteByDBID(int databaseId) => Unavailable();

    public IInterfaceRoute Add() => Unavailable<IInterfaceRoute>();

    public void Refresh()
    {
        _ = GetRoutes();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var routes = _reload();
            ArgumentNullException.ThrowIfNull(routes);
            Volatile.Write(ref _routes, routes.ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of routes from the database.",
                EFail);
        }
    }

    private IReadOnlyList<RouteAdministrationSnapshot> GetRoutes()
    {
        return Volatile.Read(ref _routes)
            ?? throw new COMException(
                "Routes access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private T Unavailable<T>()
    {
        _ = GetRoutes();
        throw new COMException(
            "This Routes member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetRoutes();
        throw new COMException(
            "This Routes member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("3FF9BB08-7924-4418-BADA-7D959467D51B")]
[ProgId("hMailServer.Route.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceRoute))]
public sealed class Route : IInterfaceRoute
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly RouteAdministrationSnapshot? _route;

    public Route()
    {
    }

    private Route(RouteAdministrationSnapshot route)
    {
        _route = route;
    }

    public int ID => Snapshot.Id;

    public string DomainName { get => Snapshot.DomainName; set => Unavailable(); }

    public string TargetSMTPHost { get => Snapshot.TargetSmtpHost; set => Unavailable(); }

    public int TargetSMTPPort { get => Snapshot.TargetSmtpPort; set => Unavailable(); }

    public int NumberOfTries { get => Snapshot.NumberOfTries; set => Unavailable(); }

    public int MinutesBetweenTry { get => Snapshot.MinutesBetweenTry; set => Unavailable(); }

    public bool AllAddresses { get => Snapshot.AllAddresses; set => Unavailable(); }

    public IInterfaceRouteAddresses Addresses =>
        RouteAddressAdministrationRuntimeHost.CreateAuthorizedAdapter(Snapshot.Id);

    public bool RelayerRequiresAuth { get => Snapshot.RelayerRequiresAuth; set => Unavailable(); }

    public string RelayerAuthUsername { get => Snapshot.RelayerAuthUsername; set => Unavailable(); }

    public bool TreatSecurityAsLocalDomain
    {
        get => Snapshot.TreatRecipientAsLocalDomain;
        set => Unavailable();
    }

    public bool UseSSL
    {
        get => Snapshot.ConnectionSecurity == (int)ComConnectionSecurity.Tls;
        set => Unavailable();
    }

    public string Description { get => Snapshot.Description; set => Unavailable(); }

    public bool TreatSenderAsLocalDomain { get => Snapshot.TreatSenderAsLocalDomain; set => Unavailable(); }

    public bool TreatRecipientAsLocalDomain { get => Snapshot.TreatRecipientAsLocalDomain; set => Unavailable(); }

    public ComConnectionSecurity ConnectionSecurity
    {
        get => (ComConnectionSecurity)Snapshot.ConnectionSecurity;
        set => Unavailable();
    }

    internal static Route CreateAuthorized(RouteAdministrationSnapshot route) => new(route);

    public void SetRelayerAuthPassword(string newValue) => Unavailable();

    public void Save() => Unavailable();

    public void Delete() => Unavailable();

    private RouteAdministrationSnapshot Snapshot =>
        _route ?? throw new COMException(
            "Route access requires an authenticated server administrator.",
            EAccessDenied);

    private T Unavailable<T>()
    {
        _ = Snapshot;
        throw new COMException(
            "This Route member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This Route member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class RouteAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IRouteAdministrationStore? _store;

    public static void Configure(IRouteAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static Routes CreateAuthorizedAdapter()
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer route administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<RouteAdministrationSnapshot> LoadRoutes() => store
            .GetRoutesAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return Routes.CreateAuthorized(LoadRoutes(), LoadRoutes);
    }
}
