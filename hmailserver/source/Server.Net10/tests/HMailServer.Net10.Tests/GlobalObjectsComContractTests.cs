using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class GlobalObjectsComContractTests
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ELegacyComError = unchecked((int)0x800403E9);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int SFalse = 1;

    [TestInitialize]
    public void ResetRuntimeHost()
    {
        DeliveryQueueAdministrationRuntimeHost.ResetForTests();
        LanguageAdministrationRuntimeHost.ResetForTests();
    }

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsCompleteVtablesAndHyperParameters()
    {
        var globalObjects = typeof(IInterfaceGlobalObjects);
        AssertDualContract(globalObjects, "F0FE3307-66D9-480D-B1B5-3AF6CD3C0C8A");
        CollectionAssert.AreEqual(
            new[] { "get_DeliveryQueue", "get_Languages" },
            MethodNames(globalObjects));
        Assert.AreEqual(1, globalObjects.GetProperty(nameof(IInterfaceGlobalObjects.DeliveryQueue))?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(2, globalObjects.GetProperty(nameof(IInterfaceGlobalObjects.Languages))?.GetCustomAttribute<DispIdAttribute>()?.Value);

        var deliveryQueue = typeof(IInterfaceDeliveryQueue);
        AssertDualContract(deliveryQueue, "B870F27A-CA77-473C-8106-A9F296F342A5");
        CollectionAssert.AreEqual(
            new[] { "Clear", "ResetDeliveryTime", "StartDelivery", "Remove" },
            MethodNames(deliveryQueue));
        Assert.AreEqual(1, deliveryQueue.GetMethod(nameof(IInterfaceDeliveryQueue.Clear))?.GetCustomAttribute<DispIdAttribute>()?.Value);
        var reset = deliveryQueue.GetMethod(nameof(IInterfaceDeliveryQueue.ResetDeliveryTime));
        Assert.AreEqual(2, reset?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(typeof(long), reset?.GetParameters()[0].ParameterType);
        Assert.AreEqual(3, deliveryQueue.GetMethod(nameof(IInterfaceDeliveryQueue.StartDelivery))?.GetCustomAttribute<DispIdAttribute>()?.Value);
        var remove = deliveryQueue.GetMethod(nameof(IInterfaceDeliveryQueue.Remove));
        Assert.AreEqual(4, remove?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(typeof(long), remove?.GetParameters()[0].ParameterType);

        var language = typeof(IInterfaceLanguage);
        AssertDualContract(language, "A98C92EF-6AA0-4F22-A29F-BE9154CC242A");
        CollectionAssert.AreEqual(
            new[] { "get_String", "get_Name", "get_IsDownloaded", "Download" },
            MethodNames(language));
        var languageString = language.GetMethod("get_String");
        Assert.AreEqual(1, languageString?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(typeof(string), languageString?.GetParameters()[0].ParameterType);
        Assert.AreEqual(2, language.GetProperty(nameof(IInterfaceLanguage.Name))?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(3, language.GetProperty(nameof(IInterfaceLanguage.IsDownloaded))?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(4, language.GetMethod(nameof(IInterfaceLanguage.Download))?.GetCustomAttribute<DispIdAttribute>()?.Value);

        var languages = typeof(IInterfaceLanguages);
        AssertDualContract(languages, "94720D8A-BC4D-493D-8BDC-8FB28BF31BA5");
        CollectionAssert.AreEqual(
            new[] { "get_Item", "get_Count", "get_ItemByName" },
            MethodNames(languages));
        var item = languages.GetProperty("Item");
        Assert.AreEqual(0, item?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(typeof(int), item?.GetIndexParameters()[0].ParameterType);
        Assert.AreEqual(1, languages.GetProperty(nameof(IInterfaceLanguages.Count))?.GetCustomAttribute<DispIdAttribute>()?.Value);
        var itemByName = languages.GetMethod("get_ItemByName");
        Assert.AreEqual(3, itemByName?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(typeof(string), itemByName?.GetParameters()[0].ParameterType);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyTypeLibraryIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<GlobalObjects>(
            "D8CC854E-F6F6-402C-ADF7-73F4569B9F8B",
            "hMailServer.GlobalObjects.1",
            typeof(IInterfaceGlobalObjects));
        AssertComClass<DeliveryQueue>(
            "27473BB7-4272-4693-ACA6-FD9D4C9C3FC5",
            "hMailServer.DeliveryQueue.1",
            typeof(IInterfaceDeliveryQueue));
        AssertComClass<Language>(
            "1C70E18B-C63D-458C-B080-64E4F94C4E83",
            "hMailServer.Language.1",
            typeof(IInterfaceLanguage));
        AssertComClass<Languages>(
            "BE1070A2-9265-495E-B134-27FAA93916CE",
            "hMailServer.Languages.1",
            typeof(IInterfaceLanguages));
        Assert.AreNotEqual(
            new Guid("200608D6-9849-49A4-9474-E7880B3E56FF"),
            typeof(GlobalObjects).GUID,
            "The stale InterfaceGlobalObjects.rgs CLSID must not replace the type-library coclass identity.");
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundaries()
    {
        var globalError = Assert.ThrowsExactly<COMException>(() => _ = new GlobalObjects().DeliveryQueue);
        var languageError = Assert.ThrowsExactly<COMException>(() => _ = new GlobalObjects().Languages);
        var clearError = Assert.ThrowsExactly<COMException>(new DeliveryQueue().Clear);
        var resetError = Assert.ThrowsExactly<COMException>(() => new DeliveryQueue().ResetDeliveryTime(long.MaxValue));
        var startError = Assert.ThrowsExactly<COMException>(new DeliveryQueue().StartDelivery);
        var removeError = Assert.ThrowsExactly<COMException>(() => new DeliveryQueue().Remove(long.MaxValue));
        var languagesCountError = Assert.ThrowsExactly<COMException>(() => _ = new Languages().Count);
        var languagesItemError = Assert.ThrowsExactly<COMException>(() => _ = new Languages()[0]);
        var languagesItemByNameError = Assert.ThrowsExactly<COMException>(() => _ = new Languages().get_ItemByName("english"));
        var languageNameError = Assert.ThrowsExactly<COMException>(() => _ = new Language().Name);
        var languageStringError = Assert.ThrowsExactly<COMException>(() => _ = new Language().get_String("Hello"));
        var languageDownloadedError = Assert.ThrowsExactly<COMException>(() => _ = new Language().IsDownloaded);
        var languageDownloadError = Assert.ThrowsExactly<COMException>(new Language().Download);
        var applicationError = Assert.ThrowsExactly<COMException>(() => _ = new Application().GlobalObjects);

        Assert.AreEqual(EAccessDenied, globalError.ErrorCode);
        Assert.AreEqual(EAccessDenied, languageError.ErrorCode);
        Assert.AreEqual(EAccessDenied, clearError.ErrorCode);
        Assert.AreEqual(EAccessDenied, resetError.ErrorCode);
        Assert.AreEqual(EAccessDenied, startError.ErrorCode);
        Assert.AreEqual(EAccessDenied, removeError.ErrorCode);
        Assert.AreEqual(EAccessDenied, languagesCountError.ErrorCode);
        Assert.AreEqual(EAccessDenied, languagesItemError.ErrorCode);
        Assert.AreEqual(EAccessDenied, languagesItemByNameError.ErrorCode);
        Assert.AreEqual(EAccessDenied, languageNameError.ErrorCode);
        Assert.AreEqual(EAccessDenied, languageStringError.ErrorCode);
        Assert.AreEqual(EAccessDenied, languageDownloadedError.ErrorCode);
        Assert.AreEqual(EAccessDenied, languageDownloadError.ErrorCode);
        Assert.AreEqual(EAccessDenied, applicationError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedGlobalObjects_ExposesQueueAndKeepsUnconfiguredLanguagesUnavailable()
    {
        var globalObjects = GlobalObjects.CreateAuthorized();
        var queue = globalObjects.DeliveryQueue;

        Assert.IsInstanceOfType<DeliveryQueue>(queue);
        AssertPending(queue.Clear);
        AssertPending(() => queue.ResetDeliveryTime(long.MaxValue));
        AssertPending(queue.StartDelivery);
        AssertPending(() => queue.Remove(long.MinValue));
        AssertPending(() => _ = globalObjects.Languages);
    }

    [TestMethod]
    public void AuthorizedGlobalObjects_ExposesLanguagesFromRuntimeStore()
    {
        var store = new RecordingLanguageAdministrationStore(
            new LanguageAdministrationSnapshot(
                "english",
                IsDownloaded: true,
                new Dictionary<string, string>
                {
                    ["Hello"] = "Hello"
                }),
            new LanguageAdministrationSnapshot(
                "turkish",
                IsDownloaded: true,
                new Dictionary<string, string>
                {
                    ["Hello"] = "Merhaba",
                    ["Empty fallback"] = string.Empty
                }));
        LanguageAdministrationRuntimeHost.Configure(store);

        var languages = GlobalObjects.CreateAuthorized().Languages;
        var turkish = languages.get_ItemByName("TURKISH");
        var missingNameError = Assert.ThrowsExactly<COMException>(() => _ = languages.get_ItemByName("missing"));
        var badIndexError = Assert.ThrowsExactly<COMException>(() => _ = languages[2]);
        var downloadError = Assert.ThrowsExactly<COMException>(turkish.Download);

        Assert.IsInstanceOfType<Languages>(languages);
        Assert.AreEqual(2, languages.Count);
        Assert.AreEqual("english", languages[0].Name);
        Assert.AreEqual("turkish", turkish.Name);
        Assert.IsTrue(turkish.IsDownloaded);
        Assert.AreEqual("Merhaba", turkish.get_String("Hello"));
        Assert.AreEqual("Empty fallback", turkish.get_String("Empty fallback"));
        Assert.AreEqual("No translation", turkish.get_String("No translation"));
        Assert.AreEqual(DispEBadIndex, missingNameError.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badIndexError.ErrorCode);
        Assert.AreEqual(ENotImplemented, downloadError.ErrorCode);
        Assert.AreEqual(1, store.CallCount);
    }

    [TestMethod]
    public void AuthenticatedApplication_ExposesAuthorizedGlobalObjectsGraph()
    {
        LanguageAdministrationRuntimeHost.Configure(
            new RecordingLanguageAdministrationStore(
                new LanguageAdministrationSnapshot("english", IsDownloaded: true, new Dictionary<string, string>())));
        var application = new Application(
            new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"));

        var denied = Assert.ThrowsExactly<COMException>(() => _ = application.GlobalObjects);
        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.IsNotNull(application.Authenticate("administrator", "secret"));

        Assert.IsInstanceOfType<GlobalObjects>(application.GlobalObjects);
        Assert.IsInstanceOfType<DeliveryQueue>(application.GlobalObjects.DeliveryQueue);
        Assert.IsInstanceOfType<Languages>(application.GlobalObjects.Languages);
        Assert.AreEqual(1, application.GlobalObjects.Languages.Count);
    }

    [TestMethod]
    public void AuthenticatedApplication_RetainedGlobalObjectsAndQueueFollowLiveAuthentication()
    {
        var store = new RecordingDeliveryQueueAdministrationStore();
        var wakeSignal = new RecordingDeliveryQueueWakeSignal();
        var clearCoordinator = new RecordingDeliveryQueueClearCoordinator();
        DeliveryQueueAdministrationRuntimeHost.Configure(store, wakeSignal, clearCoordinator);
        var application = new Application(
            new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"));

        Assert.IsNotNull(application.Authenticate("administrator", "secret"));
        var globalObjects = application.GlobalObjects;
        var queue = globalObjects.DeliveryQueue;

        Assert.IsNull(application.Authenticate("administrator", "wrong"));

        var globalObjectsError = Assert.ThrowsExactly<COMException>(() => _ = globalObjects.DeliveryQueue);
        Assert.AreEqual(EAccessDenied, globalObjectsError.ErrorCode);

        var clearError = Assert.ThrowsExactly<COMException>(queue.Clear);
        var resetError = Assert.ThrowsExactly<COMException>(() => queue.ResetDeliveryTime(1));
        var startError = Assert.ThrowsExactly<COMException>(queue.StartDelivery);
        var removeError = Assert.ThrowsExactly<COMException>(() => queue.Remove(1));

        Assert.AreEqual(ELegacyComError, clearError.ErrorCode);
        Assert.AreEqual("Server admin privileges are required to clear queue.", clearError.Message);
        Assert.AreEqual(SFalse, resetError.ErrorCode);
        Assert.AreEqual(SFalse, startError.ErrorCode);
        Assert.AreEqual(SFalse, removeError.ErrorCode);
        Assert.AreEqual(0, clearCoordinator.ScheduleCount);
        Assert.AreEqual(0, wakeSignal.SignalCount);
        Assert.IsNull(store.MessageId);
        Assert.IsNull(store.RemovedMessageId);

        Assert.IsNotNull(application.Authenticate("administrator", "secret"));

        queue.Clear();
        queue.ResetDeliveryTime(2);
        queue.StartDelivery();
        queue.Remove(3);

        Assert.AreEqual(1, clearCoordinator.ScheduleCount);
        Assert.AreEqual(1, wakeSignal.SignalCount);
        Assert.AreEqual(2, store.MessageId);
        Assert.AreEqual(3, store.RemovedMessageId);
    }

    [TestMethod]
    public void AuthorizedQueue_UsesRuntimeStoreAndWakeSignalAndPreservesPendingMethods()
    {
        var store = new RecordingDeliveryQueueAdministrationStore();
        var wakeSignal = new RecordingDeliveryQueueWakeSignal();
        var clearCoordinator = new RecordingDeliveryQueueClearCoordinator();
        DeliveryQueueAdministrationRuntimeHost.Configure(store, wakeSignal, clearCoordinator);
        var queue = GlobalObjects.CreateAuthorized().DeliveryQueue;

        queue.Clear();
        queue.ResetDeliveryTime(long.MaxValue);
        queue.StartDelivery();
        queue.Remove(long.MinValue);

        Assert.AreEqual(1, clearCoordinator.ScheduleCount);
        Assert.AreEqual(long.MaxValue, store.MessageId);
        Assert.AreEqual(long.MinValue, store.RemovedMessageId);
        Assert.AreEqual(1, wakeSignal.SignalCount);
    }

    private static void AssertDualContract(Type contract, string iid)
    {
        Assert.AreEqual(new Guid(iid), contract.GUID);
        Assert.AreEqual(ComInterfaceType.InterfaceIsDual, contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);
    }

    private static string[] MethodNames(Type contract) =>
        contract.GetMethods()
            .OrderBy(static method => method.MetadataToken)
            .Select(static method => method.Name)
            .ToArray();

    private static void AssertComClass<T>(string classId, string progId, Type defaultInterface)
    {
        var type = typeof(T);

        Assert.AreEqual(new Guid(classId), type.GUID);
        Assert.AreEqual(progId, type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(defaultInterface, type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    private static void AssertPending(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);

        Assert.AreEqual(ENotImplemented, error.ErrorCode);
    }

    private sealed class RecordingDeliveryQueueAdministrationStore : IDeliveryQueueAdministrationStore
    {
        public long? MessageId { get; private set; }

        public long? RemovedMessageId { get; private set; }

        public ValueTask<bool> ResetDeliveryTimeAsync(long messageId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MessageId = messageId;
            return ValueTask.FromResult(true);
        }

        public ValueTask<bool> RemoveAsync(
            long messageId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RemovedMessageId = messageId;
            return ValueTask.FromResult(true);
        }

        public ValueTask<int> ClearBatchAsync(
            int batchSize,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingDeliveryQueueWakeSignal : IDeliveryQueueWakeSignal
    {
        public int SignalCount { get; private set; }

        public void Signal() => SignalCount++;

        public ValueTask<bool> WaitAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingDeliveryQueueClearCoordinator : IDeliveryQueueClearCoordinator
    {
        public int ScheduleCount { get; private set; }

        public void Schedule() => ScheduleCount++;
    }

    private sealed class RecordingLanguageAdministrationStore : ILanguageAdministrationStore
    {
        private readonly IReadOnlyList<LanguageAdministrationSnapshot> _languages;

        public RecordingLanguageAdministrationStore(params LanguageAdministrationSnapshot[] languages)
        {
            _languages = languages;
        }

        public int CallCount { get; private set; }

        public ValueTask<IReadOnlyList<LanguageAdministrationSnapshot>> GetLanguagesAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(_languages);
        }
    }
}
