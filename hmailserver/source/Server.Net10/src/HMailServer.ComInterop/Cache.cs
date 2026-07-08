using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("AE45B7CD-C050-4B14-A983-30D53059D24F")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceCache
{
    [DispId(1)]
    bool Enabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(2)]
    int DomainCacheTTL { get; set; }

    [DispId(3)]
    int DomainHitRate { get; }

    [DispId(4)]
    int AccountCacheTTL { get; set; }

    [DispId(5)]
    int AccountHitRate { get; }

    [DispId(8)]
    void Clear();

    [DispId(9)]
    int AliasCacheTTL { get; set; }

    [DispId(10)]
    int AliasHitRate { get; }

    [DispId(11)]
    int DistributionListCacheTTL { get; set; }

    [DispId(12)]
    int DistributionListHitRate { get; }

    [DispId(13)]
    int DomainCacheMaxSizeKb { get; set; }

    [DispId(14)]
    int DomainCacheSizeKb { get; }

    [DispId(15)]
    int AccountCacheMaxSizeKb { get; set; }

    [DispId(16)]
    int AccountCacheSizeKb { get; }

    [DispId(17)]
    int AliasCacheMaxSizeKb { get; set; }

    [DispId(18)]
    int AliasCacheSizeKb { get; }

    [DispId(19)]
    int DistributionListCacheMaxSizeKb { get; set; }

    [DispId(20)]
    int DistributionListCacheSizeKb { get; }
}

[ComVisible(true)]
[Guid("B16F527C-116F-4F6B-B669-9A00326E255B")]
[ProgId("hMailServer.Cache.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceCache))]
public sealed class Cache : IInterfaceCache
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly CacheAdministrationSnapshot? _snapshot;
    private readonly ICacheAdministrationRuntime? _runtime;

    public Cache()
    {
    }

    private Cache(
        CacheAdministrationSnapshot snapshot,
        ICacheAdministrationRuntime? runtime)
    {
        _snapshot = snapshot;
        _runtime = runtime;
    }

    public bool Enabled { get => Snapshot.Enabled; set => Unavailable(); }

    public int DomainCacheTTL { get => Snapshot.DomainCacheTtl; set => Unavailable(); }

    public int DomainHitRate => Statistics.DomainHitRate;

    public int AccountCacheTTL { get => Snapshot.AccountCacheTtl; set => Unavailable(); }

    public int AccountHitRate => Statistics.AccountHitRate;

    public int AliasCacheTTL { get => Snapshot.AliasCacheTtl; set => Unavailable(); }

    public int AliasHitRate => Statistics.AliasHitRate;

    public int DistributionListCacheTTL { get => Snapshot.DistributionListCacheTtl; set => Unavailable(); }

    public int DistributionListHitRate => Statistics.DistributionListHitRate;

    public int DomainCacheMaxSizeKb { get => Statistics.DomainCacheMaxSizeKb; set => Unavailable(); }

    public int DomainCacheSizeKb => Statistics.DomainCacheSizeKb;

    public int AccountCacheMaxSizeKb { get => Statistics.AccountCacheMaxSizeKb; set => Unavailable(); }

    public int AccountCacheSizeKb => Statistics.AccountCacheSizeKb;

    public int AliasCacheMaxSizeKb { get => Statistics.AliasCacheMaxSizeKb; set => Unavailable(); }

    public int AliasCacheSizeKb => Statistics.AliasCacheSizeKb;

    public int DistributionListCacheMaxSizeKb { get => Statistics.DistributionListCacheMaxSizeKb; set => Unavailable(); }

    public int DistributionListCacheSizeKb => Statistics.DistributionListCacheSizeKb;

    public void Clear()
    {
        _ = Snapshot;
        try
        {
            _runtime?.Clear();
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to clear the cache.",
                EFail);
        }
    }

    internal static Cache CreateAuthorized(
        CacheAdministrationSnapshot snapshot,
        ICacheAdministrationRuntime? runtime = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new Cache(snapshot, runtime);
    }

    private CacheAdministrationSnapshot Snapshot =>
        _snapshot ?? throw new COMException(
            "Cache access requires an authenticated server administrator.",
            EAccessDenied);

    private CacheAdministrationStatistics Statistics
    {
        get
        {
            _ = Snapshot;
            if (_runtime is null)
            {
                return CacheAdministrationStatistics.Empty;
            }

            try
            {
                return _runtime.GetStatistics();
            }
            catch (Exception)
            {
                throw new COMException(
                    "It was not possible to retrieve cache statistics.",
                    EFail);
            }
        }
    }

    private T Unavailable<T>()
    {
        _ = Snapshot;
        throw new COMException(
            "This Cache member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This Cache member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public interface ICacheAdministrationRuntime
{
    void Clear();

    CacheAdministrationStatistics GetStatistics();
}

[ComVisible(false)]
public sealed record CacheAdministrationStatistics(
    int DomainHitRate,
    int AccountHitRate,
    int AliasHitRate,
    int DistributionListHitRate,
    int DomainCacheMaxSizeKb,
    int DomainCacheSizeKb,
    int AccountCacheMaxSizeKb,
    int AccountCacheSizeKb,
    int AliasCacheMaxSizeKb,
    int AliasCacheSizeKb,
    int DistributionListCacheMaxSizeKb,
    int DistributionListCacheSizeKb)
{
    public static CacheAdministrationStatistics Empty { get; } =
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}

[ComVisible(false)]
public static class CacheAdministrationRuntimeHost
{
    private static ICacheAdministrationRuntime? _runtime;

    public static void Configure(ICacheAdministrationRuntime? runtime)
    {
        Volatile.Write(ref _runtime, runtime);
    }

    internal static Cache CreateAuthorizedAdapter(CacheAdministrationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Cache.CreateAuthorized(snapshot, Volatile.Read(ref _runtime));
    }
}
