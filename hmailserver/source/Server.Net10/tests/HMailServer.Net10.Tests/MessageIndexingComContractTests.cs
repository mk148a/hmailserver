using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class MessageIndexingComContractTests
{
    [TestMethod]
    public void LegacyInterface_PreservesIidAndDispatchShape()
    {
        var contract = typeof(IInterfaceMessageIndexing);

        Assert.AreEqual(new Guid("3C58B6AD-48CC-4775-B283-53C77F717EB9"), contract.GUID);
        Assert.AreEqual(
            ComInterfaceType.InterfaceIsDual,
            contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.IsTrue(contract.GetCustomAttribute<ComVisibleAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);

        AssertMember(contract, nameof(IInterfaceMessageIndexing.TotalMessageCount), 1, typeof(int), canWrite: false);
        AssertMember(contract, nameof(IInterfaceMessageIndexing.TotalIndexedCount), 2, typeof(int), canWrite: false);
        AssertMember(contract, nameof(IInterfaceMessageIndexing.Enabled), 3, typeof(bool), canWrite: true);
        AssertMethod(contract, nameof(IInterfaceMessageIndexing.Clear), 4);
        AssertMethod(contract, nameof(IInterfaceMessageIndexing.Index), 5);

        var enabled = contract.GetProperty(nameof(IInterfaceMessageIndexing.Enabled));
        Assert.AreEqual(
            UnmanagedType.VariantBool,
            enabled?.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.VariantBool,
            enabled?.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
    }

    [TestMethod]
    public void ExtendedInterface_RemainsAdditiveAndDispatchOnly()
    {
        var contract = typeof(IInterfaceMessageIndexing2);

        Assert.AreEqual(new Guid("0B2CB0F7-897F-43FA-9D4A-56A62F7E8D72"), contract.GUID);
        Assert.AreEqual(
            ComInterfaceType.InterfaceIsIDispatch,
            contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.IsTrue(contract.GetCustomAttribute<ComVisibleAttribute>()?.Value);

        AssertMember(contract, nameof(IInterfaceMessageIndexing2.Backend), 1, typeof(string), canWrite: false);
        AssertMember(contract, nameof(IInterfaceMessageIndexing2.IsFullTextReady), 2, typeof(bool), canWrite: false);
        AssertMember(contract, nameof(IInterfaceMessageIndexing2.BackfillStatus), 3, typeof(string), canWrite: false);
        AssertMember(contract, nameof(IInterfaceMessageIndexing2.LastError), 4, typeof(string), canWrite: false);
        AssertMethod(contract, nameof(IInterfaceMessageIndexing2.Rebuild), 5);
    }

    [TestMethod]
    public void ComClass_PreservesLegacyIdentityAndDefaultInterface()
    {
        var comClass = typeof(MessageIndexing);

        Assert.AreEqual(new Guid("5F414F73-8E29-4E51-86F2-13C12EF9227A"), comClass.GUID);
        Assert.AreEqual(
            "hMailServer.MessageIndexing.1",
            comClass.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(
            ClassInterfaceType.None,
            comClass.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(
            typeof(IInterfaceMessageIndexing),
            comClass.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsTrue(comClass.GetCustomAttribute<ComVisibleAttribute>()?.Value);
        CollectionAssert.Contains(comClass.GetInterfaces(), typeof(IInterfaceMessageIndexing));
        CollectionAssert.Contains(comClass.GetInterfaces(), typeof(IInterfaceMessageIndexing2));
        Assert.IsNotNull(comClass.GetConstructor(Type.EmptyTypes));
    }

    [TestMethod]
    public void ComClass_DelegatesLegacyAndExtendedOperationsToRuntime()
    {
        var runtime = new RecordingMessageIndexingRuntime
        {
            TotalMessageCount = 12,
            TotalIndexedCount = 9,
            Enabled = false,
            Backend = "SqlServerFullText",
            IsFullTextReady = true,
            BackfillStatus = "Queued=3",
            LastError = "last error"
        };
        var adapter = new MessageIndexing(runtime);

        Assert.AreEqual(12, adapter.TotalMessageCount);
        Assert.AreEqual(9, adapter.TotalIndexedCount);
        Assert.IsFalse(adapter.Enabled);
        adapter.Enabled = true;
        adapter.Clear();
        adapter.Index();
        Assert.AreEqual("SqlServerFullText", adapter.Backend);
        Assert.IsTrue(adapter.IsFullTextReady);
        Assert.AreEqual("Queued=3", adapter.BackfillStatus);
        Assert.AreEqual("last error", adapter.LastError);
        adapter.Rebuild();

        Assert.IsTrue(runtime.Enabled);
        Assert.AreEqual(1, runtime.ClearCalls);
        Assert.AreEqual(1, runtime.IndexCalls);
        Assert.AreEqual(1, runtime.RebuildCalls);
    }

    [TestMethod]
    public void ComClass_UsesConfiguredRuntimeWhenActivatedWithoutArguments()
    {
        var runtime = new RecordingMessageIndexingRuntime
        {
            TotalMessageCount = 17
        };
        MessageIndexingRuntimeHost.Configure(runtime);

        var adapter = new MessageIndexing();

        Assert.AreEqual(17, adapter.TotalMessageCount);
    }

    private static void AssertMember(
        Type contract,
        string name,
        int dispatchId,
        Type propertyType,
        bool canWrite)
    {
        var property = contract.GetProperty(name);

        Assert.IsNotNull(property);
        Assert.AreEqual(dispatchId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(propertyType, property.PropertyType);
        Assert.AreEqual(canWrite, property.CanWrite);
    }

    private static void AssertMethod(Type contract, string name, int dispatchId)
    {
        var method = contract.GetMethod(name);

        Assert.IsNotNull(method);
        Assert.AreEqual(dispatchId, method.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(typeof(void), method.ReturnType);
        Assert.AreEqual(0, method.GetParameters().Length);
    }

    private sealed class RecordingMessageIndexingRuntime : IMessageIndexingRuntime
    {
        public int TotalMessageCount { get; init; }

        public int TotalIndexedCount { get; init; }

        public bool Enabled { get; set; }

        public string Backend { get; init; } = string.Empty;

        public bool IsFullTextReady { get; init; }

        public string BackfillStatus { get; init; } = string.Empty;

        public string LastError { get; init; } = string.Empty;

        public int ClearCalls { get; private set; }

        public int IndexCalls { get; private set; }

        public int RebuildCalls { get; private set; }

        public void Clear() => ClearCalls++;

        public void Index() => IndexCalls++;

        public void Rebuild() => RebuildCalls++;
    }
}
