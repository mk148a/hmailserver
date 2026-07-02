using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class CacheComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    [TestMethod]
    public void Interface_PreservesLegacyIidDispatchIdsMarshalingAndCompleteVtableOrder()
    {
        var contract = typeof(IInterfaceCache);

        Assert.AreEqual(new Guid("AE45B7CD-C050-4B14-A983-30D53059D24F"), contract.GUID);
        Assert.AreEqual(ComInterfaceType.InterfaceIsDual, contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);
        CollectionAssert.AreEqual(
            new[]
            {
                "get_Enabled", "set_Enabled", "get_DomainCacheTTL", "set_DomainCacheTTL",
                "get_DomainHitRate", "get_AccountCacheTTL", "set_AccountCacheTTL",
                "get_AccountHitRate", "Clear", "get_AliasCacheTTL", "set_AliasCacheTTL",
                "get_AliasHitRate", "get_DistributionListCacheTTL",
                "set_DistributionListCacheTTL", "get_DistributionListHitRate",
                "get_DomainCacheMaxSizeKb", "set_DomainCacheMaxSizeKb",
                "get_DomainCacheSizeKb", "get_AccountCacheMaxSizeKb",
                "set_AccountCacheMaxSizeKb", "get_AccountCacheSizeKb",
                "get_AliasCacheMaxSizeKb", "set_AliasCacheMaxSizeKb",
                "get_AliasCacheSizeKb", "get_DistributionListCacheMaxSizeKb",
                "set_DistributionListCacheMaxSizeKb", "get_DistributionListCacheSizeKb"
            },
            contract.GetMethods().OrderBy(static method => method.MetadataToken).Select(static method => method.Name).ToArray());

        var enabled = contract.GetProperty(nameof(IInterfaceCache.Enabled));
        Assert.AreEqual(1, enabled?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.VariantBool,
            enabled?.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.VariantBool,
            enabled?.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(8, contract.GetMethod(nameof(IInterfaceCache.Clear))?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            20,
            contract.GetProperty(nameof(IInterfaceCache.DistributionListCacheSizeKb))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClass_PreservesLegacyIdentityAndDefaultInterface()
    {
        var type = typeof(Cache);

        Assert.AreEqual(new Guid("B16F527C-116F-4F6B-B669-9A00326E255B"), type.GUID);
        Assert.AreEqual("hMailServer.Cache.1", type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(typeof(IInterfaceCache), type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var cacheError = Assert.ThrowsExactly<COMException>(() => _ = new Cache().Enabled);
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().Cache);

        Assert.AreEqual(EAccessDenied, cacheError.ErrorCode);
        Assert.AreEqual(EAccessDenied, settingsError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCache_ExposesOnlyConfiguredReadOnlyScalars()
    {
        IInterfaceCache cache = Cache.CreateAuthorized(
            new CacheAdministrationSnapshot(
                Enabled: true,
                DomainCacheTtl: 60,
                AccountCacheTtl: 90,
                AliasCacheTtl: 120,
                DistributionListCacheTtl: 180));

        Assert.IsTrue(cache.Enabled);
        Assert.AreEqual(60, cache.DomainCacheTTL);
        Assert.AreEqual(90, cache.AccountCacheTTL);
        Assert.AreEqual(120, cache.AliasCacheTTL);
        Assert.AreEqual(180, cache.DistributionListCacheTTL);

        foreach (var unavailableGetter in new Action[]
                 {
                     () => _ = cache.DomainHitRate,
                     () => _ = cache.AccountHitRate,
                     () => _ = cache.AliasHitRate,
                     () => _ = cache.DistributionListHitRate,
                     () => _ = cache.DomainCacheMaxSizeKb,
                     () => _ = cache.DomainCacheSizeKb,
                     () => _ = cache.AccountCacheMaxSizeKb,
                     () => _ = cache.AccountCacheSizeKb,
                     () => _ = cache.AliasCacheMaxSizeKb,
                     () => _ = cache.AliasCacheSizeKb,
                     () => _ = cache.DistributionListCacheMaxSizeKb,
                     () => _ = cache.DistributionListCacheSizeKb
                 })
        {
            AssertNotImplemented(unavailableGetter);
        }

        foreach (var mutation in new Action[]
                 {
                     () => cache.Enabled = false,
                     () => cache.DomainCacheTTL = 1,
                     () => cache.AccountCacheTTL = 1,
                     () => cache.AliasCacheTTL = 1,
                     () => cache.DistributionListCacheTTL = 1,
                     () => cache.DomainCacheMaxSizeKb = 1,
                     () => cache.AccountCacheMaxSizeKb = 1,
                     () => cache.AliasCacheMaxSizeKb = 1,
                     () => cache.DistributionListCacheMaxSizeKb = 1,
                     cache.Clear
                 })
        {
            AssertNotImplemented(mutation);
        }
    }

    [TestMethod]
    public void AuthorizedSettings_ExposesConfiguredCacheSnapshot()
    {
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                CacheEnabled: true,
                DomainCacheTtl: 61,
                AccountCacheTtl: 62,
                AliasCacheTtl: 63,
                DistributionListCacheTtl: 64));

        var cache = settings.Cache;

        Assert.IsTrue(cache.Enabled);
        Assert.AreEqual(61, cache.DomainCacheTTL);
        Assert.AreEqual(62, cache.AccountCacheTTL);
        Assert.AreEqual(63, cache.AliasCacheTTL);
        Assert.AreEqual(64, cache.DistributionListCacheTTL);
    }

    private static void AssertNotImplemented(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);
        Assert.AreEqual(ENotImplemented, error.ErrorCode);
    }
}
