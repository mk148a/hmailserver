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
    private const int EFail = unchecked((int)0x80004005);

    private IReadOnlyList<MessageAdministrationSnapshot>? _messages;
    private readonly IMessageAdministrationContentSource? _contentSource;
    private readonly int _accountId;
    private readonly int _folderId;
    private readonly Func<MessageAdministrationSnapshot, long>? _insert;
    private readonly Func<MessageAdministrationSnapshot, bool>? _update;
    private readonly Func<long, bool>? _delete;
    private readonly Action? _clear;
    private readonly Func<bool>? _isAuthenticated;

    public Messages()
    {
    }

    private Messages(
        IReadOnlyList<MessageAdministrationSnapshot> messages,
        IMessageAdministrationContentSource? contentSource,
        int accountId,
        int folderId,
        Func<MessageAdministrationSnapshot, long>? insert,
        Func<MessageAdministrationSnapshot, bool>? update = null,
        Func<long, bool>? delete = null,
        Action? clear = null,
        Func<bool>? isAuthenticated = null)
    {
        _messages = messages.ToArray();
        _contentSource = contentSource;
        _accountId = accountId;
        _folderId = folderId;
        _insert = insert;
        _update = update;
        _delete = delete;
        _clear = clear;
        _isAuthenticated = isAuthenticated;
    }

    public int Count => GetMessages().Count;

    internal static Messages CreateAuthorized(
        IReadOnlyList<MessageAdministrationSnapshot> messages,
        IMessageAdministrationContentSource? contentSource = null,
        int accountId = 0,
        int folderId = 0,
        Func<MessageAdministrationSnapshot, long>? insert = null,
        Func<MessageAdministrationSnapshot, bool>? update = null,
        Func<long, bool>? delete = null,
        Action? clear = null,
        Func<bool>? isAuthenticated = null)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return new Messages(messages, contentSource, accountId, folderId, insert, update, delete, clear, isAuthenticated);
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

            return Message.CreateAuthorized(
                messages[index],
                _contentSource,
                update: _update is null ? null : UpdateMessage,
                delete: _delete is null ? null : DeleteMessage,
                isAuthenticated: _isAuthenticated);
        }
    }

    public IInterfaceMessage get_ItemByDBID(long databaseId)
    {
        var match = GetMessages().FirstOrDefault(message => message.Id == databaseId);

        return match is null
            ? throw new COMException(
                "No message with the specified database identifier exists.",
                DispEBadIndex)
            : Message.CreateAuthorized(
                match,
                _contentSource,
                update: _update is null ? null : UpdateMessage,
                delete: _delete is null ? null : DeleteMessage,
                isAuthenticated: _isAuthenticated);
    }

    public void DeleteByDBID(long databaseId)
    {
        var messages = GetMessages();
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        if (!messages.Any(message => message.Id == databaseId))
        {
            return;
        }

        try
        {
            if (!_delete(databaseId))
            {
                throw new InvalidOperationException(
                    "The message delete did not affect the selected database row.");
            }

            Volatile.Write(
                ref _messages,
                messages.Where(message => message.Id != databaseId).ToArray());
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the message from the database.",
                EFail);
        }
    }

    private void DeleteMessage(long databaseId) => DeleteByDBID(databaseId);


    public IInterfaceMessage Add()
    {
        var messages = GetMessages();
        if (_folderId <= 0 || _insert is null)
        {
            throw new COMException("Message index was outside the collection.", DispEBadIndex);
        }

        return Message.CreateAuthorizedDraft(
            new MessageAdministrationSnapshot(
                Id: 0,
                AccountId: _accountId,
                FolderId: _folderId,
                FileName: string.Empty,
                State: 0,
                FromAddress: string.Empty,
                SizeBytes: 0,
                CurrentNumberOfTries: 0,
                Flags: 0,
                InternalDate: DateTime.UtcNow,
                Uid: 0),
            SaveMessage,
            publish: saved =>
            {
                Volatile.Write(ref _messages, messages.Append(saved).ToArray());
            },
            isAuthenticated: _isAuthenticated);
    }

    private bool UpdateMessage(MessageAdministrationSnapshot message)
    {
        var messages = GetMessages();
        if (_update is null)
        {
            Unavailable();
            return false;
        }

        try
        {
            if (!_update(message))
            {
                throw new InvalidOperationException(
                    "The message update did not affect the selected database row.");
            }

            var matchingIndex = Array.FindIndex(messages.ToArray(), current => current.Id == message.Id);
            if (matchingIndex >= 0)
            {
                var replaced = messages.ToArray();
                replaced[matchingIndex] = message;
                Volatile.Write(ref _messages, replaced);
            }

            return true;
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the message to the database.",
                EFail);
        }
    }

    private long SaveMessage(MessageAdministrationSnapshot message)
    {
        var messages = GetMessages();
        if (_insert is null)
        {
            Unavailable();
            return 0;
        }

        try
        {
            var insertedId = _insert(message);
            if (insertedId <= 0)
            {
                throw new InvalidOperationException(
                    "The message insert did not return a valid generated identity.");
            }

            var saved = message with { Id = insertedId };
            Volatile.Write(ref _messages, messages.Append(saved).ToArray());
            return insertedId;
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the message to the database.",
                EFail);
        }
    }


    public void Clear()
    {
        var messages = GetMessages();
        if (_clear is null)
        {
            Unavailable();
            return;
        }

        try
        {
            _clear();
            Volatile.Write(ref _messages, Array.Empty<MessageAdministrationSnapshot>());
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to clear the messages from the database.",
                EFail);
        }
    }


    private IReadOnlyList<MessageAdministrationSnapshot> GetMessages()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "Messages access requires an authenticated server administrator.",
                EAccessDenied);
        }

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

    private MessageAdministrationSnapshot? _message;
    private readonly IMessageAdministrationContentSource? _contentSource;
    private MessageContentSnapshot? _content;
    private readonly Func<MessageAdministrationSnapshot, long>? _save;
    private readonly Func<MessageAdministrationSnapshot, bool>? _update;
    private readonly Action<MessageAdministrationSnapshot>? _publish;
    private readonly Action<long>? _delete;
    private readonly Func<bool>? _isAuthenticated;
    private List<MessageHeaderSnapshot>? _draftHeaders;

    public Message()
    {
    }

    private Message(
        MessageAdministrationSnapshot message,
        IMessageAdministrationContentSource? contentSource,
        Func<MessageAdministrationSnapshot, long>? save = null,
        Func<MessageAdministrationSnapshot, bool>? update = null,
        Action<MessageAdministrationSnapshot>? publish = null,
        Action<long>? delete = null,
        Func<bool>? isAuthenticated = null)
    {
        _message = message;
        _contentSource = contentSource;
        _save = save;
        _update = update;
        _publish = publish;
        _delete = delete;
        _isAuthenticated = isAuthenticated;
    }

    public long ID => Snapshot.Id;

    public string Filename => Snapshot.FileName;

    public string Subject
    {
        get => _message is { Id: 0 } ? DraftHeaderValue("Subject") : Content.HeaderValue("Subject");
        set => SetDraftHeader("Subject", value);
    }

    public string From
    {
        get => _message is { Id: 0 } ? DraftHeaderValue("From") : Content.HeaderValue("From");
        set => SetDraftHeader("From", value);
    }

    public string Date
    {
        get => _message is { Id: 0 } ? DraftHeaderValue("Date") : Content.HeaderValue("Date");
        set => SetDraftHeader("Date", value);
    }

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
        IMessageAdministrationContentSource? contentSource = null,
        Func<MessageAdministrationSnapshot, bool>? update = null,
        Action<long>? delete = null,
        Func<bool>? isAuthenticated = null) =>
        new(message, contentSource, update: update, delete: delete, isAuthenticated: isAuthenticated);

    internal static Message CreateAuthorizedDraft(
        MessageAdministrationSnapshot message,
        Func<MessageAdministrationSnapshot, long> save,
        Action<MessageAdministrationSnapshot> publish,
        Func<bool>? isAuthenticated = null) =>
        new(message, contentSource: null, save, update: null, publish, isAuthenticated: isAuthenticated);

    public void Save()
    {
        var snapshot = Snapshot;
        if (snapshot.Id != 0)
        {
            if (_update is null)
            {
                Unavailable();
                return;
            }

            try
            {
                var from = DraftHeaderValue("From");
                var updated = snapshot with
                {
                    FromAddress = from.Length == 0 ? snapshot.FromAddress : from
                };
                if (!_update(updated))
                {
                    throw new InvalidOperationException(
                        "The message update did not affect the selected database row.");
                }

                _message = updated;
            }
            catch (COMException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new COMException(
                    "It was not possible to save the message to the database.",
                    EFail);
            }

            return;
        }

        if (_save is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var from = DraftHeaderValue("From");
            var insertSnapshot = snapshot with
            {
                FileName = string.Concat(Guid.NewGuid().ToString("N"), ".eml"),
                FromAddress = from
            };
            var insertedId = _save(insertSnapshot);
            if (insertedId <= 0)
            {
                throw new InvalidOperationException(
                    "The message insert did not return a valid generated identity.");
            }

            var saved = insertSnapshot with { Id = insertedId };
            _message = saved;
            _publish?.Invoke(saved);
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the message to the database.",
                EFail);
        }
    }

    public void Delete()
    {
        var snapshot = Snapshot;
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        _delete(snapshot.Id);
    }

    public void AddRecipient(string name, string address) => Unavailable();

    public void ClearRecipients() => Unavailable();

    public string get_HeaderValue(string fieldName) => Content.HeaderValue(fieldName ?? string.Empty);

    public void set_HeaderValue(string fieldName, string fieldValue)
    {
        if (_message is { Id: 0 })
        {
            SetDraftHeader(fieldName ?? string.Empty, fieldValue ?? string.Empty);
            return;
        }

        Unavailable();
    }

    public bool HasBodyType(string bodyType) => Content.HasBodyType(bodyType ?? string.Empty);

    public bool get_Flag(ComMessageFlag flag) => (Snapshot.Flags & (int)flag) != 0;

    public void set_Flag(ComMessageFlag flag, bool value) => Unavailable();

    public void RefreshContent() => Unavailable();

    public void Copy(int destinationFolderId) => Unavailable();

    private MessageAdministrationSnapshot Snapshot
    {
        get
        {
            if (_isAuthenticated is not null && !_isAuthenticated())
            {
                throw new COMException(
                    "Message access requires an authenticated server administrator.",
                    EAccessDenied);
            }

            return _message ?? throw new COMException(
                "Message access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

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

    private string DraftHeaderValue(string fieldName)
    {
        if (_draftHeaders is null)
        {
            return string.Empty;
        }

        var match = _draftHeaders.FirstOrDefault(
            header => string.Equals(header.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        return match?.Value ?? string.Empty;
    }

    private void SetDraftHeader(string fieldName, string value)
    {
        EnsureAuthenticated();
        if (_message is null || (_save is null && _update is null))
        {
            Unavailable();
            return;
        }

        _draftHeaders ??= new List<MessageHeaderSnapshot>();
        var existingIndex = _draftHeaders.FindIndex(
            header => string.Equals(header.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            _draftHeaders[existingIndex] = new MessageHeaderSnapshot(fieldName, value);
        }
        else
        {
            _draftHeaders.Add(new MessageHeaderSnapshot(fieldName, value));
        }
    }

    private void EnsureAuthenticated()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "Message access requires an authenticated server administrator.",
                EAccessDenied);
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
internal sealed class AccountMessageAdministrationState(int accountId)
{
    private readonly object _sync = new();
    private IReadOnlyList<MessageAdministrationSnapshot>? _messages;

    internal int AccountId { get; } = accountId;

    internal IReadOnlyList<MessageAdministrationSnapshot> GetOrLoad(
        Func<IReadOnlyList<MessageAdministrationSnapshot>> loader)
    {
        var messages = Volatile.Read(ref _messages);
        if (messages is not null)
        {
            return messages;
        }

        lock (_sync)
        {
            messages = _messages;
            if (messages is null)
            {
                messages = loader();
                ArgumentNullException.ThrowIfNull(messages);
                messages = messages.ToArray();
                Volatile.Write(ref _messages, messages);
            }

            return messages;
        }
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

    internal static AccountMessageAdministrationState CreateAuthorizedAccountState(int accountId) =>
        new(accountId);

    internal static Messages CreateAuthorizedAccountAdapter(
        AccountMessageAdministrationState state,
        Func<bool>? isAuthenticated = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer message administration runtime has not been initialized.",
                CoENotInitialized);

        var messages = state.GetOrLoad(() => store
            .GetAccountMessagesAsync(state.AccountId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult());

        return Messages.CreateAuthorized(
            messages,
            Volatile.Read(ref _contentSource),
            state.AccountId,
            folderId: -1,
            insert: _ => throw new NotSupportedException("Account message cache does not support message insertion."),
            update: _ => throw new NotSupportedException("Account message cache does not support message updates."),
            delete: _ => throw new NotSupportedException("Account message cache does not support message deletion."),
            clear: () => throw new NotSupportedException("Account message cache does not support message clear."),
            isAuthenticated: isAuthenticated);
    }

    internal static Messages CreateAuthorizedFolderAdapter(
        int folderId,
        Func<bool>? isAuthenticated = null)
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

        int accountId = messages.Count == 0 ? -1 : (int)messages[0].AccountId;

        long InsertMessage(MessageAdministrationSnapshot message) => store
            .InsertMessageAsync(accountId, folderId, message, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        bool UpdateMessage(MessageAdministrationSnapshot message) => store
            .UpdateMessageAsync(accountId, folderId, message, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        bool DeleteMessage(long messageId) => store
            .DeleteMessageAsync(accountId, folderId, messageId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        void ClearMessages() => store
            .ClearMessagesAsync(accountId, folderId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return Messages.CreateAuthorized(
            messages,
            Volatile.Read(ref _contentSource),
            accountId,
            folderId,
            InsertMessage,
            UpdateMessage,
            DeleteMessage,
            ClearMessages,
            isAuthenticated);
    }
}
