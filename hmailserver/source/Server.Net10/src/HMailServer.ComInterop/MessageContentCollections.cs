using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("0CD0DDFF-2D30-41BE-9845-D37EADB1A007")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceAttachment
{
    [DispId(1)]
    string Filename { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(2)]
    int Size { get; }

    [DispId(3)]
    void SaveAs([MarshalAs(UnmanagedType.BStr)] string name);

    [DispId(4)]
    void Delete();
}

[ComVisible(true)]
[Guid("BED37911-1180-4840-A831-196C6771EF54")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceAttachments
{
    [DispId(0)]
    IInterfaceAttachment this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    void Clear();

    [DispId(3)]
    void Add([MarshalAs(UnmanagedType.BStr)] string filename);
}

[ComVisible(true)]
[Guid("9B47C955-4462-48E3-91FE-C5E1CFEC80E0")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceRecipients
{
    [DispId(0)]
    IInterfaceRecipient this[int index] { get; }

    [DispId(1)]
    int Count { get; }
}

[ComVisible(true)]
[Guid("65D57DF8-68A1-4358-BB98-C3B33595B699")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceRecipient
{
    [DispId(1)]
    string Address { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(2)]
    bool IsLocalUser { [return: MarshalAs(UnmanagedType.VariantBool)] get; }

    [DispId(3)]
    string OriginalAddress { [return: MarshalAs(UnmanagedType.BStr)] get; }
}

[ComVisible(true)]
[Guid("FF69E250-CBFD-4AB6-9440-39599478365D")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceMessageHeader
{
    [DispId(1)]
    string Name { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(2)]
    string Value { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(3)]
    void Delete();
}

[ComVisible(true)]
[Guid("1ADE0B5E-536C-4707-8385-32A7F6F92500")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceMessageHeaders
{
    [DispId(0)]
    IInterfaceMessageHeader this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    [SpecialName]
    IInterfaceMessageHeader get_ItemByName([MarshalAs(UnmanagedType.BStr)] string name);
}

[ComVisible(true)]
[Guid("63FF738A-982B-41E6-87C7-BA4AA9622B30")]
[ProgId("hMailServer.Attachments.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceAttachments))]
public sealed class Attachments : IInterfaceAttachments
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<MessageAttachmentSnapshot>? _attachments;

    public Attachments()
    {
    }

    private Attachments(IReadOnlyList<MessageAttachmentSnapshot> attachments)
    {
        _attachments = attachments.ToArray();
    }

    public int Count => GetAttachments().Count;

    internal static Attachments CreateAuthorized(IReadOnlyList<MessageAttachmentSnapshot> attachments) => new(attachments);

    public IInterfaceAttachment this[int index]
    {
        get
        {
            var attachments = GetAttachments();
            if (index < 0 || index >= attachments.Count)
            {
                throw new COMException("Attachment index was outside the collection.", DispEBadIndex);
            }

            return Attachment.CreateAuthorized(attachments[index]);
        }
    }

    public void Clear() => Unavailable();

    public void Add(string filename) => Unavailable();

    private IReadOnlyList<MessageAttachmentSnapshot> GetAttachments() =>
        _attachments ?? throw new COMException(
            "Attachments access requires an authenticated server administrator.",
            EAccessDenied);

    private void Unavailable()
    {
        _ = GetAttachments();
        throw new COMException(
            "This Attachments member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("B65A156A-54D1-4803-80CE-273F44AE935F")]
[ProgId("hMailServer.Attachment.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceAttachment))]
public sealed class Attachment : IInterfaceAttachment
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly MessageAttachmentSnapshot? _attachment;

    public Attachment()
    {
    }

    private Attachment(MessageAttachmentSnapshot attachment)
    {
        _attachment = attachment;
    }

    public string Filename => Snapshot.FileName;

    public int Size => Snapshot.Size;

    internal static Attachment CreateAuthorized(MessageAttachmentSnapshot attachment) => new(attachment);

    public void SaveAs(string name) => Unavailable();

    public void Delete() => Unavailable();

    private MessageAttachmentSnapshot Snapshot =>
        _attachment ?? throw new COMException(
            "Attachment access requires an authenticated server administrator.",
            EAccessDenied);

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This Attachment member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("B5B9C42D-64F1-443F-AA0D-FABB2DD9317B")]
[ProgId("hMailServer.Recipients.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceRecipients))]
public sealed class Recipients : IInterfaceRecipients
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);

    private readonly IReadOnlyList<MessageRecipientSnapshot>? _recipients;

    public Recipients()
    {
    }

    private Recipients(IReadOnlyList<MessageRecipientSnapshot> recipients)
    {
        _recipients = recipients.ToArray();
    }

    public int Count => GetRecipients().Count;

    internal static Recipients CreateAuthorized(IReadOnlyList<MessageRecipientSnapshot> recipients) => new(recipients);

    public IInterfaceRecipient this[int index]
    {
        get
        {
            var recipients = GetRecipients();
            if (index < 0 || index >= recipients.Count)
            {
                throw new COMException("Recipient index was outside the collection.", DispEBadIndex);
            }

            return Recipient.CreateAuthorized(recipients[index]);
        }
    }

    private IReadOnlyList<MessageRecipientSnapshot> GetRecipients() =>
        _recipients ?? throw new COMException(
            "Recipients access requires an authenticated server administrator.",
            EAccessDenied);
}

[ComVisible(true)]
[Guid("45B82F51-8445-4F3A-BC9E-137FC04BFE2A")]
[ProgId("hMailServer.Recipient.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceRecipient))]
public sealed class Recipient : IInterfaceRecipient
{
    private const int EAccessDenied = unchecked((int)0x80070005);

    private readonly MessageRecipientSnapshot? _recipient;

    public Recipient()
    {
    }

    private Recipient(MessageRecipientSnapshot recipient)
    {
        _recipient = recipient;
    }

    public string Address => Snapshot.Address;

    public bool IsLocalUser => Snapshot.IsLocalUser;

    public string OriginalAddress => Snapshot.OriginalAddress;

    internal static Recipient CreateAuthorized(MessageRecipientSnapshot recipient) => new(recipient);

    private MessageRecipientSnapshot Snapshot =>
        _recipient ?? throw new COMException(
            "Recipient access requires an authenticated server administrator.",
            EAccessDenied);
}

[ComVisible(true)]
[Guid("AE360CD2-BB40-4B39-83A6-84516C865365")]
[ProgId("hMailServer.MessageHeaders.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceMessageHeaders))]
public sealed class MessageHeaders : IInterfaceMessageHeaders
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);

    private readonly IReadOnlyList<MessageHeaderSnapshot>? _headers;

    public MessageHeaders()
    {
    }

    private MessageHeaders(IReadOnlyList<MessageHeaderSnapshot> headers)
    {
        _headers = headers.ToArray();
    }

    public int Count => GetHeaders().Count;

    internal static MessageHeaders CreateAuthorized(IReadOnlyList<MessageHeaderSnapshot> headers) => new(headers);

    public IInterfaceMessageHeader this[int index]
    {
        get
        {
            var headers = GetHeaders();
            if (index < 0 || index >= headers.Count)
            {
                throw new COMException("Message header index was outside the collection.", DispEBadIndex);
            }

            return MessageHeader.CreateAuthorized(headers[index]);
        }
    }

    public IInterfaceMessageHeader get_ItemByName(string name)
    {
        var match = GetHeaders().FirstOrDefault(
            header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No message header with the specified name exists.", DispEBadIndex)
            : MessageHeader.CreateAuthorized(match);
    }

    private IReadOnlyList<MessageHeaderSnapshot> GetHeaders() =>
        _headers ?? throw new COMException(
            "MessageHeaders access requires an authenticated server administrator.",
            EAccessDenied);
}

[ComVisible(true)]
[Guid("983EE030-380D-4E39-850D-AA543F3C1CB9")]
[ProgId("hMailServer.MessageHeader.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceMessageHeader))]
public sealed class MessageHeader : IInterfaceMessageHeader
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly MessageHeaderSnapshot? _header;

    public MessageHeader()
    {
    }

    private MessageHeader(MessageHeaderSnapshot header)
    {
        _header = header;
    }

    public string Name { get => Snapshot.Name; set => Unavailable(); }

    public string Value { get => Snapshot.Value; set => Unavailable(); }

    internal static MessageHeader CreateAuthorized(MessageHeaderSnapshot header) => new(header);

    public void Delete() => Unavailable();

    private MessageHeaderSnapshot Snapshot =>
        _header ?? throw new COMException(
            "MessageHeader access requires an authenticated server administrator.",
            EAccessDenied);

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This MessageHeader member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

internal sealed record MessageAttachmentSnapshot(string FileName, int Size);

internal sealed record MessageRecipientSnapshot(string Address, string OriginalAddress, bool IsLocalUser);

internal sealed record MessageHeaderSnapshot(string Name, string Value);
