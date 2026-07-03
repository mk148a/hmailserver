using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("1259E989-465E-4B63-BB0B-4DB7F6244ACE")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceMessages
{
    [DispId(0)]
    IInterfaceMessage this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    [SpecialName]
    IInterfaceMessage get_ItemByDBID(long databaseId);

    [DispId(3)]
    void DeleteByDBID(long databaseId);

    [DispId(4)]
    IInterfaceMessage Add();

    [DispId(5)]
    void Clear();
}

[ComVisible(true)]
[Guid("8C054031-7B42-485C-BF79-3D94A7B9605F")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceMessage
{
    [DispId(1)]
    long ID { get; }

    [DispId(2)]
    string Filename { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(3)]
    string Subject { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(4)]
    string From { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(5)]
    string Date { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(6)]
    string Body { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(7)]
    string HTMLBody { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(8)]
    IInterfaceAttachments Attachments { get; }

    [DispId(9)]
    void Save();

    [DispId(10)]
    string To { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(11)]
    void AddRecipient(
        [MarshalAs(UnmanagedType.BStr)] string name,
        [MarshalAs(UnmanagedType.BStr)] string address);

    [DispId(12)]
    string FromAddress { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(13)]
    int State { get; }

    [DispId(14)]
    int Size { get; }

    [DispId(15)]
    void ClearRecipients();

    [DispId(16)]
    string CC { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(17)]
    IInterfaceRecipients Recipients { get; }

    [DispId(18)]
    [return: MarshalAs(UnmanagedType.BStr)]
    string get_HeaderValue([MarshalAs(UnmanagedType.BStr)] string fieldName);

    [DispId(18)]
    void set_HeaderValue(
        [MarshalAs(UnmanagedType.BStr)] string fieldName,
        [MarshalAs(UnmanagedType.BStr)] string fieldValue);

    [DispId(19)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool HasBodyType([MarshalAs(UnmanagedType.BStr)] string bodyType);

    [DispId(20)]
    bool EncodeFields
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(21)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool get_Flag(ComMessageFlag flag);

    [DispId(21)]
    void set_Flag(
        ComMessageFlag flag,
        [MarshalAs(UnmanagedType.VariantBool)] bool value);

    [DispId(22)]
    object InternalDate { [return: MarshalAs(UnmanagedType.Struct)] get; }

    [DispId(23)]
    IInterfaceMessageHeaders Headers { get; }

    [DispId(24)]
    void RefreshContent();

    [DispId(25)]
    int DeliveryAttempt { get; }

    [DispId(26)]
    string Charset { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(27)]
    void Copy(int destinationFolderId);

    [DispId(28)]
    int UID { get; }
}

[ComVisible(true)]
[Guid("C04047AD-45A4-48EA-907E-2C270C95409C")]
[ProgId("hMailServer.Messages.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceMessages))]
public sealed class Messages : IInterfaceMessages
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<MessageAdministrationSnapshot>? _messages;

    public Messages()
    {
    }

    private Messages(IReadOnlyList<MessageAdministrationSnapshot> messages)
    {
        _messages = messages.ToArray();
    }

    public int Count => GetMessages().Count;

    internal static Messages CreateAuthorized(IReadOnlyList<MessageAdministrationSnapshot> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return new Messages(messages);
    }

    public IInterfaceMessage this[int index]
    {
        get
        {
            var messages = GetMessages();
            if (index < 0 || index >= messages.Count)
            {
                throw new COMException("Message index was outside the collection.", DispEBadIndex);
            }

            return Message.CreateAuthorized(messages[index]);
        }
    }

    public IInterfaceMessage get_ItemByDBID(long databaseId)
    {
        var match = GetMessages().FirstOrDefault(message => message.Id == databaseId);

        return match is null
            ? throw new COMException(
                "No message with the specified database identifier exists.",
                DispEBadIndex)
            : Message.CreateAuthorized(match);
    }

    public void DeleteByDBID(long databaseId) => Unavailable();

    public IInterfaceMessage Add() => Unavailable<IInterfaceMessage>();

    public void Clear() => Unavailable();

    private IReadOnlyList<MessageAdministrationSnapshot> GetMessages()
    {
        return _messages
            ?? throw new COMException(
                "Messages access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private T Unavailable<T>()
    {
        _ = GetMessages();
        throw new COMException(
            "This Messages member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetMessages();
        throw new COMException(
            "This Messages member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("61B2C7D7-3814-441F-9574-EE2CC9829447")]
[ProgId("hMailServer.Message.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceMessage))]
public sealed class Message : IInterfaceMessage
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly MessageAdministrationSnapshot? _message;

    public Message()
    {
    }

    private Message(MessageAdministrationSnapshot message)
    {
        _message = message;
    }

    public long ID => Snapshot.Id;

    public string Filename => Snapshot.FileName;

    public string Subject { get => Unavailable<string>(); set => Unavailable(); }

    public string From { get => Unavailable<string>(); set => Unavailable(); }

    public string Date { get => Unavailable<string>(); set => Unavailable(); }

    public string Body { get => Unavailable<string>(); set => Unavailable(); }

    public string HTMLBody { get => Unavailable<string>(); set => Unavailable(); }

    public IInterfaceAttachments Attachments => Unavailable<IInterfaceAttachments>();

    public string To => Unavailable<string>();

    public string FromAddress { get => Snapshot.FromAddress; set => Unavailable(); }

    public int State => Snapshot.State;

    public int Size => unchecked((int)(Snapshot.SizeBytes / 1024));

    public string CC => Unavailable<string>();

    public IInterfaceRecipients Recipients => Unavailable<IInterfaceRecipients>();

    public bool EncodeFields { get => Unavailable<bool>(); set => Unavailable(); }

    public object InternalDate => Snapshot.InternalDate;

    public IInterfaceMessageHeaders Headers => Unavailable<IInterfaceMessageHeaders>();

    public int DeliveryAttempt => Snapshot.CurrentNumberOfTries + 1;

    public string Charset { get => Unavailable<string>(); set => Unavailable(); }

    public int UID => unchecked((int)Snapshot.Uid);

    internal static Message CreateAuthorized(MessageAdministrationSnapshot message) => new(message);

    public void Save() => Unavailable();

    public void AddRecipient(string name, string address) => Unavailable();

    public void ClearRecipients() => Unavailable();

    public string get_HeaderValue(string fieldName) => Unavailable<string>();

    public void set_HeaderValue(string fieldName, string fieldValue) => Unavailable();

    public bool HasBodyType(string bodyType) => Unavailable<bool>();

    public bool get_Flag(ComMessageFlag flag) => (Snapshot.Flags & (int)flag) != 0;

    public void set_Flag(ComMessageFlag flag, bool value) => Unavailable();

    public void RefreshContent() => Unavailable();

    public void Copy(int destinationFolderId) => Unavailable();

    private MessageAdministrationSnapshot Snapshot =>
        _message ?? throw new COMException(
            "Message access requires an authenticated server administrator.",
            EAccessDenied);

    private T Unavailable<T>()
    {
        _ = Snapshot;
        throw new COMException(
            "This Message member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This Message member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class MessageAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IMessageAdministrationStore? _store;

    public static void Configure(IMessageAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static Messages CreateAuthorizedAccountAdapter(int accountId)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer message administration runtime has not been initialized.",
                CoENotInitialized);

        var messages = store
            .GetAccountMessagesAsync(accountId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return Messages.CreateAuthorized(messages);
    }

    internal static Messages CreateAuthorizedFolderAdapter(int folderId)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer message administration runtime has not been initialized.",
                CoENotInitialized);

        var messages = store
            .GetFolderMessagesAsync(folderId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return Messages.CreateAuthorized(messages);
    }
}
