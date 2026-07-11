using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class CacheComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
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
        var cacheClearError = Assert.ThrowsExactly<COMException>(new Cache().Clear);
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().Cache);

        Assert.AreEqual(EAccessDenied, cacheError.ErrorCode);
        Assert.AreEqual(EAccessDenied, cacheClearError.ErrorCode);
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
        AssertDefaultStatistics(cache);

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
                     () => cache.DistributionListCacheMaxSizeKb = 1
                 })
        {
            AssertNotImplemented(mutation);
        }

        cache.Clear();
    }

    [TestMethod]
    public void AuthorizedCache_ClearUsesRuntimeBoundaryAndMapsContainedFailure()
    {
        var runtime = new RecordingCacheAdministrationRuntime();
        IInterfaceCache cache = Cache.CreateAuthorized(
            new CacheAdministrationSnapshot(
                Enabled: true,
                DomainCacheTtl: 60,
                AccountCacheTtl: 90,
                AliasCacheTtl: 120,
                DistributionListCacheTtl: 180),
            runtime);

        cache.Clear();

        Assert.AreEqual(1, runtime.ClearCount);

        runtime.ThrowOnClear = true;
        var error = Assert.ThrowsExactly<COMException>(cache.Clear);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(2, runtime.ClearCount);

        runtime.ThrowOnClear = false;
        runtime.Statistics = new CacheAdministrationStatistics(
            DomainHitRate: 11,
            AccountHitRate: 22,
            AliasHitRate: 33,
            DistributionListHitRate: 44,
            DomainCacheMaxSizeKb: 100,
            DomainCacheSizeKb: 10,
            AccountCacheMaxSizeKb: 200,
            AccountCacheSizeKb: 20,
            AliasCacheMaxSizeKb: 300,
            AliasCacheSizeKb: 30,
            DistributionListCacheMaxSizeKb: 400,
            DistributionListCacheSizeKb: 40);

        AssertStatistics(cache, runtime.Statistics);

        runtime.ThrowOnStatistics = true;
        var statisticsError = Assert.ThrowsExactly<COMException>(() => _ = cache.DomainHitRate);

        Assert.AreEqual(EFail, statisticsError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedSettings_ExposesConfiguredCacheSnapshot()
    {
        var runtime = new RecordingCacheAdministrationRuntime();
        CacheAdministrationRuntimeHost.Configure(runtime);
        try
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
            AssertDefaultStatistics(cache);

            cache.Clear();

            Assert.AreEqual(1, runtime.ClearCount);
        }
        finally
        {
            CacheAdministrationRuntimeHost.Configure(null);
        }
    }

    [TestMethod]
    public void FailedReauthentication_RevokesSettingsCacheClearAndHitRatesOnly()
    {
        var runtime = new RecordingCacheAdministrationRuntime
        {
            Statistics = new CacheAdministrationStatistics(
                DomainHitRate: 11,
                AccountHitRate: 22,
                AliasHitRate: 33,
                DistributionListHitRate: 44,
                DomainCacheMaxSizeKb: 100,
                DomainCacheSizeKb: 10,
                AccountCacheMaxSizeKb: 200,
                AccountCacheSizeKb: 20,
                AliasCacheMaxSizeKb: 300,
                AliasCacheSizeKb: 30,
                DistributionListCacheMaxSizeKb: 400,
                DistributionListCacheSizeKb: 40)
        };
        var settingsStore = new RecordingSettingsAdministrationStore(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                CacheEnabled: true,
                DomainCacheTtl: 60,
                AccountCacheTtl: 90,
                AliasCacheTtl: 120,
                DistributionListCacheTtl: 180));
        CacheAdministrationRuntimeHost.Configure(runtime);
        SettingsAdministrationRuntimeHost.Configure(settingsStore);
        var application = Application.CreateForRuntime(new TestAdministratorAuthenticationProvider("secret"));

        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var settings = application.Settings;
        var cache = settings.Cache;

        Assert.AreEqual(1, settingsStore.ReadCount);
        Assert.IsNull(application.Authenticate("Administrator", "wrong"));

        var applicationSettingsError = Assert.ThrowsExactly<COMException>(() => _ = application.Settings);
        var retainedSettingsError = Assert.ThrowsExactly<COMException>(() => _ = settings.Cache);

        Assert.AreEqual(EAccessDenied, applicationSettingsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, retainedSettingsError.ErrorCode);
        Assert.AreEqual(1, settingsStore.ReadCount);

        Assert.IsTrue(cache.Enabled);
        Assert.AreEqual(60, cache.DomainCacheTTL);
        Assert.AreEqual(100, cache.DomainCacheMaxSizeKb);
        Assert.AreEqual(10, cache.DomainCacheSizeKb);
        var statisticsReadsBeforeProtectedCalls = runtime.StatisticsReadCount;

        foreach (var hitRate in new Func<int>[]
                 {
                     () => cache.DomainHitRate,
                     () => cache.AccountHitRate,
                     () => cache.AliasHitRate,
                     () => cache.DistributionListHitRate
                 })
        {
            var error = Assert.ThrowsExactly<COMException>(() => _ = hitRate());
            Assert.AreEqual(EAccessDenied, error.ErrorCode);
        }

        var clearError = Assert.ThrowsExactly<COMException>(cache.Clear);

        Assert.AreEqual(EAccessDenied, clearError.ErrorCode);
        Assert.AreEqual(statisticsReadsBeforeProtectedCalls, runtime.StatisticsReadCount);
        Assert.AreEqual(0, runtime.ClearCount);
    }

    private static void AssertNotImplemented(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);
        Assert.AreEqual(ENotImplemented, error.ErrorCode);
    }

    private static void AssertDefaultStatistics(IInterfaceCache cache)
    {
        AssertStatistics(cache, CacheAdministrationStatistics.Empty);
    }

    private static void AssertStatistics(
        IInterfaceCache cache,
        CacheAdministrationStatistics statistics)
    {
        Assert.AreEqual(statistics.DomainHitRate, cache.DomainHitRate);
        Assert.AreEqual(statistics.AccountHitRate, cache.AccountHitRate);
        Assert.AreEqual(statistics.AliasHitRate, cache.AliasHitRate);
        Assert.AreEqual(statistics.DistributionListHitRate, cache.DistributionListHitRate);
        Assert.AreEqual(statistics.DomainCacheMaxSizeKb, cache.DomainCacheMaxSizeKb);
        Assert.AreEqual(statistics.DomainCacheSizeKb, cache.DomainCacheSizeKb);
        Assert.AreEqual(statistics.AccountCacheMaxSizeKb, cache.AccountCacheMaxSizeKb);
        Assert.AreEqual(statistics.AccountCacheSizeKb, cache.AccountCacheSizeKb);
        Assert.AreEqual(statistics.AliasCacheMaxSizeKb, cache.AliasCacheMaxSizeKb);
        Assert.AreEqual(statistics.AliasCacheSizeKb, cache.AliasCacheSizeKb);
        Assert.AreEqual(statistics.DistributionListCacheMaxSizeKb, cache.DistributionListCacheMaxSizeKb);
        Assert.AreEqual(statistics.DistributionListCacheSizeKb, cache.DistributionListCacheSizeKb);
    }

    private sealed class RecordingCacheAdministrationRuntime : ICacheAdministrationRuntime
    {
        public CacheAdministrationStatistics Statistics { get; set; } = CacheAdministrationStatistics.Empty;

        public int ClearCount { get; private set; }

        public int StatisticsReadCount { get; private set; }

        public bool ThrowOnClear { get; set; }

        public bool ThrowOnStatistics { get; set; }

        public void Clear()
        {
            ClearCount++;
            if (ThrowOnClear)
            {
                throw new InvalidOperationException("Simulated cache clear failure.");
            }
        }

        public CacheAdministrationStatistics GetStatistics()
        {
            StatisticsReadCount++;
            if (ThrowOnStatistics)
            {
                throw new InvalidOperationException("Simulated cache statistics failure.");
            }

            return Statistics;
        }
    }

    private sealed class RecordingSettingsAdministrationStore(SettingsAdministrationSnapshot snapshot)
        : ISettingsAdministrationStore
    {
        public int ReadCount { get; private set; }

        public ValueTask<SettingsAdministrationSnapshot> GetSettingsAsync(CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult(snapshot);
        }
    }

    private sealed class TestAdministratorAuthenticationProvider(string password)
        : IServerAdministratorAuthenticationProvider
    {
        public bool Authenticate(string username, string attemptedPassword) =>
            string.Equals(username, "Administrator", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(attemptedPassword, password, StringComparison.Ordinal);
    }
}
