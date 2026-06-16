using System.Globalization;
using System.Text;
using MimeKit;

namespace HMailServer.Protocols.Imap;

public static class ImapMimeFetchFormatter
{
    public static string FormatEnvelope(byte[]? rawMessage)
    {
        return FormatEnvelope(LoadMessage(rawMessage));
    }

    private static string FormatEnvelope(MimeMessage message)
    {
        return string.Concat(
            "(",
            FormatNString(message.Date == default ? null : message.Date.ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss +0000", CultureInfo.InvariantCulture)),
            " ",
            FormatNString(message.Subject),
            " ",
            FormatAddressList(message.From),
            " ",
            FormatAddressList(message.Sender is null ? message.From : [message.Sender]),
            " ",
            FormatAddressList(message.ReplyTo.Count == 0 ? message.From : message.ReplyTo),
            " ",
            FormatAddressList(message.To),
            " ",
            FormatAddressList(message.Cc),
            " ",
            FormatAddressList(message.Bcc),
            " ",
            FormatNString(message.InReplyTo),
            " ",
            FormatNString(message.MessageId),
            ")");
    }

    public static string FormatBodyStructure(byte[]? rawMessage)
    {
        var message = LoadMessage(rawMessage);
        return FormatEntity(message.Body ?? new TextPart("plain") { Text = string.Empty });
    }

    private static MimeMessage LoadMessage(byte[]? rawMessage)
    {
        if (rawMessage is null)
        {
            throw new InvalidOperationException("Message content file is missing.");
        }

        try
        {
            using var stream = new MemoryStream(rawMessage);
            return MimeMessage.Load(stream);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Message content file could not be parsed as MIME.", ex);
        }
    }

    private static string FormatEntity(MimeEntity entity)
    {
        if (entity is Multipart multipart)
        {
            return FormatMultipart(multipart);
        }

        if (entity is MessagePart messagePart)
        {
            return FormatMessagePart(messagePart);
        }

        if (entity is TextPart textPart)
        {
            return FormatTextPart(textPart);
        }

        if (entity is MimePart mimePart)
        {
            return FormatMimePart(mimePart);
        }

        return "(\"APPLICATION\" \"OCTET-STREAM\" NIL NIL NIL \"7BIT\" 0 NIL NIL NIL)";
    }

    private static string FormatMultipart(Multipart multipart)
    {
        var builder = new StringBuilder();
        builder.Append('(');
        foreach (var part in multipart)
        {
            builder.Append(FormatEntity(part));
        }

        builder.Append(' ')
            .Append(FormatQuoted(multipart.ContentType.MediaSubtype.ToUpperInvariant()))
            .Append(' ')
            .Append(FormatParameters(multipart.ContentType.Parameters))
            .Append(' ')
            .Append(FormatDisposition(multipart.ContentDisposition))
            .Append(" NIL NIL)");
        return builder.ToString();
    }

    private static string FormatMessagePart(MessagePart part)
    {
        var nested = part.Message ?? new MimeMessage();
        var body = nested.Body ?? new TextPart("plain") { Text = string.Empty };
        return string.Concat(
            "(\"MESSAGE\" \"RFC822\" ",
            FormatParameters(part.ContentType.Parameters),
            " ",
            FormatNString(part.ContentId),
            " NIL ",
            FormatNString("7BIT"),
            " ",
            GetEntitySize(part).ToString(CultureInfo.InvariantCulture),
            " ",
            FormatEnvelope(nested),
            " ",
            FormatEntity(body),
            " ",
            CountTextLines(nested.TextBody ?? string.Empty).ToString(CultureInfo.InvariantCulture),
            " NIL ",
            FormatDisposition(part.ContentDisposition),
            " NIL NIL)");
    }

    private static string FormatTextPart(TextPart part)
    {
        var text = part.Text ?? string.Empty;
        return string.Concat(
            "(\"TEXT\" ",
            FormatQuoted(part.ContentType.MediaSubtype.ToUpperInvariant()),
            " ",
            FormatParameters(part.ContentType.Parameters),
            " ",
            FormatNString(part.ContentId),
            " NIL ",
            FormatNString(FormatEncoding(part.ContentTransferEncoding)),
            " ",
            GetEntitySize(part).ToString(CultureInfo.InvariantCulture),
            " ",
            CountTextLines(text).ToString(CultureInfo.InvariantCulture),
            " NIL ",
            FormatDisposition(part.ContentDisposition),
            " NIL NIL)");
    }

    private static string FormatMimePart(MimePart part)
    {
        return string.Concat(
            "(",
            FormatQuoted(part.ContentType.MediaType.ToUpperInvariant()),
            " ",
            FormatQuoted(part.ContentType.MediaSubtype.ToUpperInvariant()),
            " ",
            FormatParameters(part.ContentType.Parameters),
            " ",
            FormatNString(part.ContentId),
            " NIL ",
            FormatNString(FormatEncoding(part.ContentTransferEncoding)),
            " ",
            GetEntitySize(part).ToString(CultureInfo.InvariantCulture),
            " NIL ",
            FormatDisposition(part.ContentDisposition),
            " NIL NIL)");
    }

    private static long GetEntitySize(MimeEntity entity)
    {
        using var stream = new MemoryStream();
        entity.WriteTo(stream);
        return stream.Length;
    }

    private static long CountTextLines(string text)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        long lines = 1;
        foreach (var current in text)
        {
            if (current == '\n')
            {
                lines++;
            }
        }

        return lines;
    }

    private static string FormatEncoding(ContentEncoding encoding) =>
        encoding == ContentEncoding.Default
            ? "7BIT"
            : encoding.ToString().ToUpperInvariant();

    private static string FormatAddressList(InternetAddressList addresses)
    {
        var mailboxes = addresses.Mailboxes.ToArray();
        if (mailboxes.Length == 0)
        {
            return "NIL";
        }

        return "(" + string.Concat(mailboxes.Select(FormatAddress)) + ")";
    }

    private static string FormatAddress(MailboxAddress address)
    {
        var mailbox = address.Address;
        var at = mailbox.LastIndexOf('@');
        var localPart = at > 0 ? mailbox[..at] : mailbox;
        var host = at > 0 && at < mailbox.Length - 1 ? mailbox[(at + 1)..] : string.Empty;
        return string.Concat(
            "(",
            FormatNString(address.Name),
            " NIL ",
            FormatNString(localPart),
            " ",
            FormatNString(host),
            ")");
    }

    private static string FormatParameters(ParameterList parameters)
    {
        var values = parameters
            .Where(static parameter => !string.IsNullOrWhiteSpace(parameter.Name))
            .Select(parameter => FormatQuoted(parameter.Name.ToUpperInvariant()) + " " + FormatQuoted(parameter.Value ?? string.Empty))
            .ToArray();
        return values.Length == 0 ? "NIL" : "(" + string.Join(' ', values) + ")";
    }

    private static string FormatDisposition(ContentDisposition? disposition)
    {
        if (disposition is null)
        {
            return "NIL";
        }

        return "(" + FormatQuoted(disposition.Disposition.ToUpperInvariant()) + " " + FormatParameters(disposition.Parameters) + ")";
    }

    private static string FormatNString(string? value) =>
        string.IsNullOrEmpty(value) ? "NIL" : FormatQuoted(value);

    private static string FormatQuoted(string value) =>
        "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal) + "\"";
}
