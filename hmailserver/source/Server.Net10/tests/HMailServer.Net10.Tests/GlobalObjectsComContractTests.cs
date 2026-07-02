using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class GlobalObjectsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    [TestInitialize]
    public void ResetRuntimeHost() => DeliveryQueueAdministrationRuntimeHost.ResetForTests();

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
        Assert.AreEqual(new Guid("94720D8A-BC4D-493D-8BDC-8FB28BF31BA5"), typeof(IInterfaceLanguages).GUID);

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
        var applicationError = Assert.ThrowsExactly<COMException>(() => _ = new Application().GlobalObjects);

        Assert.AreEqual(EAccessDenied, globalError.ErrorCode);
        Assert.AreEqual(EAccessDenied, languageError.ErrorCode);
        Assert.AreEqual(EAccessDenied, clearError.ErrorCode);
        Assert.AreEqual(EAccessDenied, resetError.ErrorCode);
        Assert.AreEqual(EAccessDenied, startError.ErrorCode);
        Assert.AreEqual(EAccessDenied, removeError.ErrorCode);
        Assert.AreEqual(EAccessDenied, applicationError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedGlobalObjects_ExposesQueueAndKeepsPendingMembersUnavailable()
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
    public void AuthenticatedApplication_ExposesAuthorizedGlobalObjectsGraph()
    {
        var application = new Application(
            new LegacyServerAdministratorAuthenticationProvider("5ebe2294ecd0e0f08eab7690d2a6ee69"));

        var denied = Assert.ThrowsExactly<COMException>(() => _ = application.GlobalObjects);
        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.IsNotNull(application.Authenticate("administrator", "secret"));

        Assert.IsInstanceOfType<GlobalObjects>(application.GlobalObjects);
        Assert.IsInstanceOfType<DeliveryQueue>(application.GlobalObjects.DeliveryQueue);
    }

    [TestMethod]
    public void AuthorizedQueue_UsesRuntimeStoreAndWakeSignalAndPreservesPendingMethods()
    {
        var store = new RecordingDeliveryQueueAdministrationStore();
        var wakeSignal = new RecordingDeliveryQueueWakeSignal();
        DeliveryQueueAdministrationRuntimeHost.Configure(store, wakeSignal);
        var queue = GlobalObjects.CreateAuthorized().DeliveryQueue;

        queue.ResetDeliveryTime(long.MaxValue);
        queue.StartDelivery();

        Assert.AreEqual(long.MaxValue, store.MessageId);
        Assert.AreEqual(1, wakeSignal.SignalCount);
        AssertPending(queue.Clear);
        AssertPending(() => queue.Remove(long.MaxValue));
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

        public ValueTask<bool> ResetDeliveryTimeAsync(long messageId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MessageId = messageId;
            return ValueTask.FromResult(true);
        }
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
}
