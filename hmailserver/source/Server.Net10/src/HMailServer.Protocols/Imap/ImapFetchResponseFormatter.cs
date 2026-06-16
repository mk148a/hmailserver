using System.Globalization;
using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public static class ImapFetchResponseFormatter
{
    private static readonly Encoding ResponseEncoding = Encoding.ASCII;

    public static byte[] Format(
        IReadOnlyList<ImapFetchedMessage> messages,
        IReadOnlyList<ImapFetchDataItem> items,
        string tag)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        using var output = new MemoryStream();
        foreach (var message in messages)
        {
            AppendAscii(output, "* ");
            AppendAscii(output, message.SequenceNumber.ToString(CultureInfo.InvariantCulture));
            AppendAscii(output, " FETCH (");

            for (var index = 0; index < items.Count; index++)
            {
                if (index > 0)
                {
                    AppendAscii(output, " ");
                }

                AppendItem(output, message, items[index]);
            }

            AppendAscii(output, ")\r\n");
        }

        AppendAscii(output, SanitizeAtom(tag));
        AppendAscii(output, " OK FETCH completed\r\n");
        return output.ToArray();
    }

    private static void AppendItem(
        MemoryStream output,
        ImapFetchedMessage message,
        ImapFetchDataItem item)
    {
        switch (item)
        {
            case ImapFetchDataItem.Flags:
                AppendAscii(output, "FLAGS ");
                AppendAscii(output, FormatFlags(message.Flags));
                break;

            case ImapFetchDataItem.Uid:
                AppendAscii(output, "UID ");
                AppendAscii(output, message.Identity.Uid.ToString(CultureInfo.InvariantCulture));
                break;

            case ImapFetchDataItem.Rfc822Size:
                AppendAscii(output, "RFC822.SIZE ");
                AppendAscii(output, message.SizeBytes.ToString(CultureInfo.InvariantCulture));
                break;

            case ImapFetchDataItem.InternalDate:
                AppendAscii(output, "INTERNALDATE \"");
                AppendAscii(output, message.InternalDateUtc.ToUniversalTime().ToString("dd-MMM-yyyy HH:mm:ss +0000", CultureInfo.InvariantCulture));
                AppendAscii(output, "\"");
                break;

            case ImapFetchDataItem.Envelope:
                AppendAscii(output, "ENVELOPE ");
                AppendAscii(output, ImapMimeFetchFormatter.FormatEnvelope(message.RawMessage));
                break;

            case ImapFetchDataItem.BodyStructure:
                AppendAscii(output, "BODYSTRUCTURE ");
                AppendAscii(output, ImapMimeFetchFormatter.FormatBodyStructure(message.RawMessage));
                break;

            case ImapFetchDataItem.Body:
            case ImapFetchDataItem.BodyPeek:
                AppendLiteral(output, "BODY[]", message.RawMessage);
                break;

            case ImapFetchDataItem.Rfc822:
                AppendLiteral(output, "RFC822", message.RawMessage);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(item), item, "Unknown IMAP FETCH data item.");
        }
    }

    private static void AppendLiteral(MemoryStream output, string name, byte[]? value)
    {
        if (value is null)
        {
            throw new InvalidOperationException("Message content file is missing.");
        }

        AppendAscii(output, name);
        AppendAscii(output, " {");
        AppendAscii(output, value.Length.ToString(CultureInfo.InvariantCulture));
        AppendAscii(output, "}\r\n");
        output.Write(value);
    }

    public static string FormatFlags(byte flags)
    {
        var names = new List<string>(capacity: 6);
        if ((flags & ImapMessageFlags.Deleted) == ImapMessageFlags.Deleted)
        {
            names.Add("\\Deleted");
        }

        if ((flags & ImapMessageFlags.Seen) == ImapMessageFlags.Seen)
        {
            names.Add("\\Seen");
        }

        if ((flags & ImapMessageFlags.Draft) == ImapMessageFlags.Draft)
        {
            names.Add("\\Draft");
        }

        if ((flags & ImapMessageFlags.Answered) == ImapMessageFlags.Answered)
        {
            names.Add("\\Answered");
        }

        if ((flags & ImapMessageFlags.Flagged) == ImapMessageFlags.Flagged)
        {
            names.Add("\\Flagged");
        }

        if ((flags & ImapMessageFlags.Recent) == ImapMessageFlags.Recent)
        {
            names.Add("\\Recent");
        }

        return "(" + string.Join(' ', names) + ")";
    }

    private static void AppendAscii(MemoryStream output, string value)
    {
        var bytes = ResponseEncoding.GetBytes(value);
        output.Write(bytes);
    }

    private static string SanitizeAtom(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);
}
