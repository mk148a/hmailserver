using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;
using MimeKit;

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
    private readonly IMessageAdministrationContentSource? _contentSource;

    public Messages()
    {
    }

    private Messages(
        IReadOnlyList<MessageAdministrationSnapshot> messages,
        IMessageAdministrationContentSource? contentSource)
    {
        _messages = messages.ToArray();
        _contentSource = contentSource;
    }

    public int Count => GetMessages().Count;

    internal static Messages CreateAuthorized(
        IReadOnlyList<MessageAdministrationSnapshot> messages,
        IMessageAdministrationContentSource? contentSource = null)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return new Messages(messages, contentSource);
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

            return Message.CreateAuthorized(messages[index], _contentSource);
        }
    }

    public IInterfaceMessage get_ItemByDBID(long databaseId)
    {
        var match = GetMessages().FirstOrDefault(message => message.Id == databaseId);

        return match is null
            ? throw new COMException(
                "No message with the specified database identifier exists.",
                DispEBadIndex)
            : Message.CreateAuthorized(match, _contentSource);
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

    private const int EFail = unchecked((int)0x80004005);

    private readonly MessageAdministrationSnapshot? _message;
    private readonly IMessageAdministrationContentSource? _contentSource;
    private MessageContentSnapshot? _content;

    public Message()
    {
    }

    private Message(
        MessageAdministrationSnapshot message,
        IMessageAdministrationContentSource? contentSource)
    {
        _message = message;
        _contentSource = contentSource;
    }

    public long ID => Snapshot.Id;

    public string Filename => Snapshot.FileName;

    public string Subject { get => Content.HeaderValue("Subject"); set => Unavailable(); }

    public string From { get => Content.HeaderValue("From"); set => Unavailable(); }

    public string Date { get => Content.HeaderValue("Date"); set => Unavailable(); }

    public string Body { get => Content.TextBody; set => Unavailable(); }

    public string HTMLBody { get => Content.HtmlBody; set => Unavailable(); }

    public IInterfaceAttachments Attachments => HMailServer.ComInterop.Attachments.CreateAuthorized(Content.Attachments);

    public string To => Content.HeaderValue("To");

    public string FromAddress { get => Snapshot.FromAddress; set => Unavailable(); }

    public int State => Snapshot.State;

    public int Size => unchecked((int)(Snapshot.SizeBytes / 1024));

    public string CC
    {
        get
        {
            var value = Content.HeaderValue("Cc");
            return value.Length == 0 ? Content.HeaderValue("CC") : value;
        }
    }

    public IInterfaceRecipients Recipients => HMailServer.ComInterop.Recipients.CreateAuthorized(Content.Recipients);

    public bool EncodeFields { get => Unavailable<bool>(); set => Unavailable(); }

    public object InternalDate => Snapshot.InternalDate;

    public IInterfaceMessageHeaders Headers => MessageHeaders.CreateAuthorized(Content.Headers);

    public int DeliveryAttempt => Snapshot.CurrentNumberOfTries + 1;

    public string Charset { get => Content.Charset; set => Unavailable(); }

    public int UID => unchecked((int)Snapshot.Uid);

    internal static Message CreateAuthorized(
        MessageAdministrationSnapshot message,
        IMessageAdministrationContentSource? contentSource = null) =>
        new(message, contentSource);

    public void Save() => Unavailable();

    public void AddRecipient(string name, string address) => Unavailable();

    public void ClearRecipients() => Unavailable();

    public string get_HeaderValue(string fieldName) => Content.HeaderValue(fieldName ?? string.Empty);

    public void set_HeaderValue(string fieldName, string fieldValue) => Unavailable();

    public bool HasBodyType(string bodyType) => Content.HasBodyType(bodyType ?? string.Empty);

    public bool get_Flag(ComMessageFlag flag) => (Snapshot.Flags & (int)flag) != 0;

    public void set_Flag(ComMessageFlag flag, bool value) => Unavailable();

    public void RefreshContent() => Unavailable();

    public void Copy(int destinationFolderId) => Unavailable();

    private MessageAdministrationSnapshot Snapshot =>
        _message ?? throw new COMException(
            "Message access requires an authenticated server administrator.",
            EAccessDenied);

    private MessageContentSnapshot Content
    {
        get
        {
            _ = Snapshot;
            if (_content is not null)
            {
                return _content;
            }

            if (_contentSource is null)
            {
                throw new COMException(
                    "This Message member is not implemented by the .NET 10 rewrite yet.",
                    ENotImplemented);
            }

            var content = _contentSource
                .TryLoadMessageAsync(Snapshot, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            if (content is null)
            {
                throw new COMException("The message content file is unavailable.", EFail);
            }

            _content = MessageContentSnapshot.Parse(content);
            return _content;
        }
    }

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

internal sealed class MessageContentSnapshot
{
    private const int EFail = unchecked((int)0x80004005);

    private MessageContentSnapshot(
        IReadOnlyList<MessageHeaderSnapshot> headers,
        IReadOnlyList<MessageRecipientSnapshot> recipients,
        IReadOnlyList<MessageAttachmentSnapshot> attachments,
        string textBody,
        string htmlBody,
        string charset,
        MimeEntity? body)
    {
        Headers = headers;
        Recipients = recipients;
        Attachments = attachments;
        TextBody = textBody;
        HtmlBody = htmlBody;
        Charset = charset;
        Body = body;
    }

    public IReadOnlyList<MessageHeaderSnapshot> Headers { get; }

    public IReadOnlyList<MessageRecipientSnapshot> Recipients { get; }

    public IReadOnlyList<MessageAttachmentSnapshot> Attachments { get; }

    public string TextBody { get; }

    public string HtmlBody { get; }

    public string Charset { get; }

    private MimeEntity? Body { get; }

    public static MessageContentSnapshot Parse(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        MimeMessage message;
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            message = MimeMessage.Load(stream);
        }
        catch (FormatException)
        {
            throw new COMException("The message content file could not be parsed.", EFail);
        }

        return new MessageContentSnapshot(
            message.Headers
                .Select(static header => new MessageHeaderSnapshot(header.Field, header.Value))
                .ToArray(),
            GetRecipients(message).ToArray(),
            message.Attachments
                .Select(static (attachment, index) => new MessageAttachmentSnapshot(
                    GetAttachmentFileName(attachment, index),
                    GetAttachmentSize(attachment)))
                .ToArray(),
            string.IsNullOrEmpty(message.TextBody) ? FindTextBody(message.Body, "plain") ?? string.Empty : message.TextBody,
            string.IsNullOrEmpty(message.HtmlBody) ? FindTextBody(message.Body, "html") ?? string.Empty : message.HtmlBody,
            message.Body?.ContentType?.Charset ?? string.Empty,
            message.Body);
    }

    public string HeaderValue(string fieldName)
    {
        var match = Headers.FirstOrDefault(
            header => string.Equals(header.Name, fieldName, StringComparison.OrdinalIgnoreCase));

        return match?.Value ?? string.Empty;
    }

    public bool HasBodyType(string bodyType)
    {
        if (string.IsNullOrWhiteSpace(bodyType))
        {
            return false;
        }

        return HasBodyType(Body, bodyType);
    }

    private static bool HasBodyType(MimeEntity? entity, string bodyType)
    {
        if (entity is null)
        {
            return false;
        }

        if (string.Equals(entity.ContentType?.MimeType, bodyType, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (entity is Multipart multipart)
        {
            foreach (var child in multipart)
            {
                if (HasBodyType(child, bodyType))
                {
                    return true;
                }
            }
        }
        else if (entity is MessagePart messagePart)
        {
            return HasBodyType(messagePart.Message?.Body, bodyType);
        }

        return false;
    }

    private static string? FindTextBody(MimeEntity? entity, string mediaSubtype)
    {
        if (entity is null)
        {
            return null;
        }

        if (entity is TextPart textPart
            && string.Equals(textPart.ContentType.MediaSubtype, mediaSubtype, StringComparison.OrdinalIgnoreCase))
        {
            return textPart.Text;
        }

        if (entity is Multipart multipart)
        {
            foreach (var child in multipart)
            {
                var match = FindTextBody(child, mediaSubtype);
                if (match is not null)
                {
                    return match;
                }
            }
        }
        else if (entity is MessagePart messagePart)
        {
            return FindTextBody(messagePart.Message?.Body, mediaSubtype);
        }

        return null;
    }

    private static IEnumerable<MessageRecipientSnapshot> GetRecipients(MimeMessage message)
    {
        foreach (var recipient in GetRecipients(message.To))
        {
            yield return recipient;
        }

        foreach (var recipient in GetRecipients(message.Cc))
        {
            yield return recipient;
        }

        foreach (var recipient in GetRecipients(message.Bcc))
        {
            yield return recipient;
        }
    }

    private static IEnumerable<MessageRecipientSnapshot> GetRecipients(InternetAddressList addresses)
    {
        foreach (var mailbox in addresses.Mailboxes)
        {
            yield return new MessageRecipientSnapshot(
                mailbox.Address,
                mailbox.Address,
                IsLocalUser: false);
        }
    }

    private static string GetAttachmentFileName(MimeEntity attachment, int index)
    {
        var fileName = attachment.ContentDisposition?.FileName
            ?? attachment.ContentType?.Name;

        return string.IsNullOrWhiteSpace(fileName)
            ? "attachment-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : fileName;
    }

    private static int GetAttachmentSize(MimeEntity attachment)
    {
        using var output = new MemoryStream();
        if (attachment is MimePart part && part.Content is not null)
        {
            part.Content.DecodeTo(output);
        }
        else
        {
            attachment.WriteTo(output);
        }

        return unchecked((int)output.Length);
    }
}

[ComVisible(false)]
public static class MessageAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IMessageAdministrationStore? _store;
    private static IMessageAdministrationContentSource? _contentSource;

    public static void Configure(
        IMessageAdministrationStore store,
        IMessageAdministrationContentSource? contentSource = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
        Volatile.Write(ref _contentSource, contentSource);
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

        return Messages.CreateAuthorized(messages, Volatile.Read(ref _contentSource));
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

        return Messages.CreateAuthorized(messages, Volatile.Read(ref _contentSource));
    }
}
