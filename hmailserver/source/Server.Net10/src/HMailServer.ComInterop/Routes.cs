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
    private readonly Func<RouteAdministrationSnapshot, int>? _insert;
    private readonly Func<bool>? _isServerAdministrator;

    public Routes()
    {
    }

    private Routes(
        IReadOnlyList<RouteAdministrationSnapshot> routes,
        Func<IReadOnlyList<RouteAdministrationSnapshot>>? reload,
        Func<RouteAdministrationSnapshot, int>? insert,
        Func<bool>? isServerAdministrator)
    {
        _routes = routes.ToArray();
        _reload = reload;
        _insert = insert;
        _isServerAdministrator = isServerAdministrator;
    }

    public int Count => GetRoutes().Count;

    internal static Routes CreateAuthorized(
        IReadOnlyList<RouteAdministrationSnapshot> routes,
        Func<IReadOnlyList<RouteAdministrationSnapshot>>? reload = null,
        Func<RouteAdministrationSnapshot, int>? insert = null,
        Func<bool>? isServerAdministrator = null)
    {
        ArgumentNullException.ThrowIfNull(routes);
        return new Routes(routes, reload, insert, isServerAdministrator);
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

            return Route.CreateAuthorized(
                routes[index],
                save: _insert is null ? null : SaveRoute,
                isServerAdministrator: _isServerAdministrator);
        }
    }

    public IInterfaceRoute get_ItemByName(string itemName)
    {
        var match = GetRoutes().FirstOrDefault(
            route => string.Equals(route.DomainName, itemName, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No route with the specified domain name exists.", DispEBadIndex)
            : Route.CreateAuthorized(
                match,
                save: _insert is null ? null : SaveRoute,
                isServerAdministrator: _isServerAdministrator);
    }

    public IInterfaceRoute get_ItemByDBID(int databaseId)
    {
        var match = GetRoutes().FirstOrDefault(route => route.Id == databaseId);

        return match is null
            ? throw new COMException("No route with the specified database identifier exists.", DispEBadIndex)
            : Route.CreateAuthorized(
                match,
                save: _insert is null ? null : SaveRoute,
                isServerAdministrator: _isServerAdministrator);
    }

    public void DeleteByDBID(int databaseId) => Unavailable();

    public IInterfaceRoute Add()
    {
        _ = GetRoutes();
        EnsureServerAdministrator();
        if (_insert is null)
        {
            return Unavailable<IInterfaceRoute>();
        }

        return Route.CreateAuthorized(
            new RouteAdministrationSnapshot(
                Id: 0,
                DomainName: string.Empty,
                Description: string.Empty,
                TargetSmtpHost: string.Empty,
                TargetSmtpPort: 0,
                NumberOfTries: 0,
                MinutesBetweenTry: 0,
                AllAddresses: true,
                RelayerRequiresAuth: false,
                RelayerAuthUsername: string.Empty,
                TreatRecipientAsLocalDomain: false,
                TreatSenderAsLocalDomain: false,
                ConnectionSecurity: 0),
            save: SaveRoute,
            isServerAdministrator: _isServerAdministrator);
    }

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

    private RouteAdministrationSnapshot SaveRoute(RouteAdministrationSnapshot route)
    {
        EnsureServerAdministrator();
        var routes = GetRoutes();
        if (route.Id != 0 || _insert is null)
        {
            Unavailable();
            return route;
        }

        try
        {
            var insertedId = _insert(route);
            if (insertedId <= 0)
            {
                throw new InvalidOperationException(
                    "The route insert did not return a valid generated identity.");
            }

            var insertedRoute = route with { Id = insertedId };
            Volatile.Write(ref _routes, routes.Append(insertedRoute).ToArray());
            return insertedRoute;
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the route to the database.",
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

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "Routes access requires an authenticated server administrator.",
                EAccessDenied);
        }
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
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private RouteAdministrationSnapshot? _route;
    private readonly Func<RouteAdministrationSnapshot, RouteAdministrationSnapshot>? _save;
    private readonly Func<bool>? _isServerAdministrator;

    public Route()
    {
    }

    private Route(
        RouteAdministrationSnapshot route,
        Func<RouteAdministrationSnapshot, RouteAdministrationSnapshot>? save,
        Func<bool>? isServerAdministrator)
    {
        _route = route;
        _save = save;
        _isServerAdministrator = isServerAdministrator;
    }

    public int ID => Snapshot.Id;

    public string DomainName { get => Snapshot.DomainName; set => Mutate(route => route with { DomainName = value ?? string.Empty }); }

    public string TargetSMTPHost { get => Snapshot.TargetSmtpHost; set => Mutate(route => route with { TargetSmtpHost = value ?? string.Empty }); }

    public int TargetSMTPPort { get => Snapshot.TargetSmtpPort; set => Mutate(route => route with { TargetSmtpPort = value }); }

    public int NumberOfTries { get => Snapshot.NumberOfTries; set => Mutate(route => route with { NumberOfTries = value }); }

    public int MinutesBetweenTry { get => Snapshot.MinutesBetweenTry; set => Mutate(route => route with { MinutesBetweenTry = value }); }

    public bool AllAddresses { get => Snapshot.AllAddresses; set => Mutate(route => route with { AllAddresses = value }); }

    public IInterfaceRouteAddresses Addresses =>
        RouteAddressAdministrationRuntimeHost.CreateAuthorizedAdapter(
            Snapshot.Id,
            _isServerAdministrator);

    public bool RelayerRequiresAuth { get => Snapshot.RelayerRequiresAuth; set => Mutate(route => route with { RelayerRequiresAuth = value }); }

    public string RelayerAuthUsername { get => Snapshot.RelayerAuthUsername; set => Mutate(route => route with { RelayerAuthUsername = value ?? string.Empty }); }

    public bool TreatSecurityAsLocalDomain
    {
        get => Snapshot.TreatRecipientAsLocalDomain;
        set => Mutate(route => route with { TreatRecipientAsLocalDomain = value });
    }

    public bool UseSSL
    {
        get => Snapshot.ConnectionSecurity == (int)ComConnectionSecurity.Tls;
        set => Mutate(route => route with { ConnectionSecurity = value ? (int)ComConnectionSecurity.Tls : (int)ComConnectionSecurity.None });
    }

    public string Description { get => Snapshot.Description; set => Mutate(route => route with { Description = value ?? string.Empty }); }

    public bool TreatSenderAsLocalDomain { get => Snapshot.TreatSenderAsLocalDomain; set => Mutate(route => route with { TreatSenderAsLocalDomain = value }); }

    public bool TreatRecipientAsLocalDomain { get => Snapshot.TreatRecipientAsLocalDomain; set => Mutate(route => route with { TreatRecipientAsLocalDomain = value }); }

    public ComConnectionSecurity ConnectionSecurity
    {
        get => (ComConnectionSecurity)Snapshot.ConnectionSecurity;
        set => Mutate(route => route with { ConnectionSecurity = (int)value });
    }

    internal static Route CreateAuthorized(
        RouteAdministrationSnapshot route,
        Func<RouteAdministrationSnapshot, RouteAdministrationSnapshot>? save = null,
        Func<bool>? isServerAdministrator = null) =>
        new(route, save, isServerAdministrator);

    public void SetRelayerAuthPassword(string newValue) =>
        Mutate(route => route with { RelayerAuthPassword = newValue ?? string.Empty });

    public void Save()
    {
        EnsureServerAdministrator();
        var snapshot = Snapshot;
        if (_save is null)
        {
            Unavailable();
            return;
        }

        try
        {
            _route = _save(snapshot);
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the route to the database.",
                EFail);
        }
    }

    public void Delete() => Unavailable();

    private RouteAdministrationSnapshot Snapshot =>
        _route ?? throw new COMException(
            "Route access requires an authenticated server administrator.",
            EAccessDenied);

    private void Mutate(Func<RouteAdministrationSnapshot, RouteAdministrationSnapshot> mutation)
    {
        EnsureServerAdministrator();
        if (_save is null)
        {
            Unavailable();
            return;
        }

        _route = mutation(Snapshot);
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "Route access requires an authenticated server administrator.",
                EAccessDenied);
        }
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

    internal static Routes CreateAuthorizedAdapter(Func<bool>? isServerAdministrator = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer route administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<RouteAdministrationSnapshot> LoadRoutes() =>
            store
                .GetRoutesAsync(CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();

        int InsertRoute(RouteAdministrationSnapshot route) =>
            store
                .InsertRouteAsync(route, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();

        return Routes.CreateAuthorized(
            LoadRoutes(),
            LoadRoutes,
            InsertRoute,
            isServerAdministrator);
    }
}