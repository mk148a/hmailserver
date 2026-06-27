using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ServerMessagesComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceServerMessages),
            "0E90D7D8-0144-4021-9240-8CB9CC6F7628",
            new[] { "get_Item", "get_Count", "get_ItemByDBID", "get_ItemByName", "Refresh" });
        Assert.AreEqual(
            0,
            typeof(IInterfaceServerMessages).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            4,
            typeof(IInterfaceServerMessages).GetMethod(nameof(IInterfaceServerMessages.Refresh))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceServerMessage),
            "6F7C0387-1AC5-466B-9068-67D659D57A86",
            new[] { "get_ID", "get_Name", "set_Name", "Save", "get_Text", "set_Text" });
        Assert.AreEqual(
            4,
            typeof(IInterfaceServerMessage).GetProperty(nameof(IInterfaceServerMessage.Text))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<ServerMessages>(
            "379F1428-A4C9-4D43-9745-AEABF8950755",
            "hMailServer.ServerMessages.1",
            typeof(IInterfaceServerMessages));
        AssertComClass<ServerMessage>(
            "561076C6-9174-43D3-B889-CFCC42E3AE5E",
            "hMailServer.ServerMessage.1",
            typeof(IInterfaceServerMessage));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var messagesError = Assert.ThrowsExactly<COMException>(() => _ = new ServerMessages().Count);
        var messageError = Assert.ThrowsExactly<COMException>(() => _ = new ServerMessage().Name);
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().ServerMessages);

        Assert.AreEqual(EAccessDenied, messagesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, messageError.ErrorCode);
        Assert.AreEqual(EAccessDenied, settingsError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlySnapshotsAndLegacyLookupErrors()
    {
        IInterfaceServerMessages messages = ServerMessages.CreateAuthorized(
            new[]
            {
                Snapshot(10, "MESSAGE_UNDELIVERABLE", "Message undeliverable"),
                Snapshot(20, "VIRUS_FOUND", "Virus found")
            });

        Assert.AreEqual(2, messages.Count);
        AssertMessage(messages[0], 10, "MESSAGE_UNDELIVERABLE", "Message undeliverable");
        AssertMessage(messages.get_ItemByDBID(20), 20, "VIRUS_FOUND", "Virus found");
        AssertMessage(messages.get_ItemByName("virus_found"), 20, "VIRUS_FOUND", "Virus found");

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = messages[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = messages.get_ItemByDBID(30));
        var badName = Assert.ThrowsExactly<COMException>(() => _ = messages.get_ItemByName("Missing"));
        var pendingRefresh = Assert.ThrowsExactly<COMException>(messages.Refresh);
        var pendingNameMutation = Assert.ThrowsExactly<COMException>(() => messages[0].Name = "Changed");
        var pendingTextMutation = Assert.ThrowsExactly<COMException>(() => messages[0].Text = "Changed");
        var pendingSave = Assert.ThrowsExactly<COMException>(messages[0].Save);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badName.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingNameMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingTextMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSave.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedSettings_UsesConfiguredServerMessageRuntime()
    {
        ServerMessageAdministrationRuntimeHost.Configure(
            new FixedServerMessageAdministrationStore(
                new[]
                {
                    Snapshot(20, "VIRUS_FOUND", "Virus found"),
                    Snapshot(10, "MESSAGE_UNDELIVERABLE", "Message undeliverable")
                }));
        IInterfaceSettings settings = Settings.CreateAuthorized();

        var messages = settings.ServerMessages;

        Assert.AreEqual(2, messages.Count);
        Assert.AreEqual("MESSAGE_UNDELIVERABLE", messages[0].Name);
        Assert.AreEqual("Message undeliverable", messages[0].Text);
    }

    private static ServerMessageAdministrationSnapshot Snapshot(int id, string name, string text) =>
        new(id, name, text);

    private static void AssertMessage(
        IInterfaceServerMessage message,
        int id,
        string name,
        string text)
    {
        Assert.AreEqual(id, message.ID);
        Assert.AreEqual(name, message.Name);
        Assert.AreEqual(text, message.Text);
    }

    private static void AssertContract(Type contract, string interfaceId, string[] methodNames)
    {
        Assert.AreEqual(new Guid(interfaceId), contract.GUID);
        Assert.AreEqual(ComInterfaceType.InterfaceIsDual, contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);
        CollectionAssert.AreEqual(
            methodNames,
            contract.GetMethods().OrderBy(static method => method.MetadataToken).Select(static method => method.Name).ToArray());
    }

    private static void AssertComClass<T>(string classId, string progId, Type defaultInterface)
    {
        var type = typeof(T);

        Assert.AreEqual(new Guid(classId), type.GUID);
        Assert.AreEqual(progId, type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(defaultInterface, type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    private sealed class FixedServerMessageAdministrationStore(
        IReadOnlyList<ServerMessageAdministrationSnapshot> messages)
        : IServerMessageAdministrationStore
    {
        public ValueTask<IReadOnlyList<ServerMessageAdministrationSnapshot>> GetServerMessagesAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ServerMessageAdministrationSnapshot>>(
                messages.OrderBy(static message => message.Name, StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
