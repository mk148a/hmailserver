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
}
