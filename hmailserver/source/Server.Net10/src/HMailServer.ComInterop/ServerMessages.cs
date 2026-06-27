using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("0E90D7D8-0144-4021-9240-8CB9CC6F7628")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceServerMessages
{
    [DispId(0)]
    IInterfaceServerMessage this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    [SpecialName]
    IInterfaceServerMessage get_ItemByDBID(int databaseId);

    [DispId(3)]
    [SpecialName]
    IInterfaceServerMessage get_ItemByName([MarshalAs(UnmanagedType.BStr)] string name);

    [DispId(4)]
    void Refresh();
}

[ComVisible(true)]
[Guid("6F7C0387-1AC5-466B-9068-67D659D57A86")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceServerMessage
{
    [DispId(1)]
    int ID { get; }

    [DispId(2)]
    string Name { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(3)]
    void Save();

    [DispId(4)]
    string Text { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }
}

[ComVisible(true)]
[Guid("379F1428-A4C9-4D43-9745-AEABF8950755")]
[ProgId("hMailServer.ServerMessages.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceServerMessages))]
public sealed class ServerMessages : IInterfaceServerMessages
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<ServerMessageAdministrationSnapshot>? _messages;

    public ServerMessages()
    {
    }

    private ServerMessages(IReadOnlyList<ServerMessageAdministrationSnapshot> messages)
    {
        _messages = messages.ToArray();
    }

    public int Count => GetMessages().Count;

    internal static ServerMessages CreateAuthorized(
        IReadOnlyList<ServerMessageAdministrationSnapshot> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return new ServerMessages(messages);
    }

    public IInterfaceServerMessage this[int index]
    {
        get
        {
            var messages = GetMessages();
            if (index < 0 || index >= messages.Count)
            {
                throw new COMException("Server message index was outside the collection.", DispEBadIndex);
            }

            return ServerMessage.CreateAuthorized(messages[index]);
        }
    }

    public IInterfaceServerMessage get_ItemByDBID(int databaseId)
    {
        var match = GetMessages().FirstOrDefault(message => message.Id == databaseId);

        return match is null
            ? throw new COMException(
                "No server message with the specified database identifier exists.",
                DispEBadIndex)
            : ServerMessage.CreateAuthorized(match);
    }

    public IInterfaceServerMessage get_ItemByName(string name)
    {
        var match = GetMessages().FirstOrDefault(
            message => string.Equals(message.Name, name, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No server message with the specified name exists.", DispEBadIndex)
            : ServerMessage.CreateAuthorized(match);
    }

    public void Refresh() => Unavailable();

    private IReadOnlyList<ServerMessageAdministrationSnapshot> GetMessages()
    {
        return _messages
            ?? throw new COMException(
                "ServerMessages access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private void Unavailable()
    {
        _ = GetMessages();
        throw new COMException(
            "This ServerMessages member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("561076C6-9174-43D3-B889-CFCC42E3AE5E")]
[ProgId("hMailServer.ServerMessage.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceServerMessage))]
public sealed class ServerMessage : IInterfaceServerMessage
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly ServerMessageAdministrationSnapshot? _message;

    public ServerMessage()
    {
    }

    private ServerMessage(ServerMessageAdministrationSnapshot message)
    {
        _message = message;
    }

    public int ID => Snapshot.Id;

    public string Name { get => Snapshot.Name; set => Unavailable(); }

    public string Text { get => Snapshot.Text; set => Unavailable(); }

    internal static ServerMessage CreateAuthorized(ServerMessageAdministrationSnapshot message) => new(message);

    public void Save() => Unavailable();

    private ServerMessageAdministrationSnapshot Snapshot =>
        _message ?? throw new COMException(
            "ServerMessage access requires an authenticated server administrator.",
            EAccessDenied);

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This ServerMessage member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class ServerMessageAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IServerMessageAdministrationStore? _store;

    public static void Configure(IServerMessageAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static ServerMessages CreateAuthorizedAdapter()
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer server-message administration runtime has not been initialized.",
                CoENotInitialized);

        var messages = store
            .GetServerMessagesAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return ServerMessages.CreateAuthorized(messages);
    }
}
