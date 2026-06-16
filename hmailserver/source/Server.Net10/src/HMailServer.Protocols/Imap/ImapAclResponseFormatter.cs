using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public static class ImapAclResponseFormatter
{
    private const string ListRights = "l r s w i k x t e a";

    public static string FormatGetAcl(ImapAclListResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var builder = new StringBuilder();
        builder.Append("* ACL ")
            .Append(Quote(result.MailboxName));

        foreach (var entry in result.Entries)
        {
            builder.Append(' ')
                .Append(FormatIdentifier(entry.Identifier))
                .Append(' ')
                .Append(entry.Rights);
        }

        builder.Append("\r\n");
        return builder.ToString();
    }

    public static string FormatMyRights(ImapAclRightsResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return "* MYRIGHTS " + Quote(result.MailboxName) + " " + result.Rights + "\r\n";
    }

    public static string FormatListRights(string mailboxName, string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mailboxName);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        return "* LISTRIGHTS " +
            Quote(mailboxName) +
            " " +
            FormatIdentifier(identifier) +
            " " +
            ListRights +
            "\r\n";
    }

    private static string FormatIdentifier(string identifier) =>
        IsAtom(identifier) ? identifier : Quote(identifier);

    private static string Quote(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return "\"" + escaped + "\"";
    }

    private static bool IsAtom(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character <= ' ' || character is '(' or ')' or '{' or '"' or '\\')
            {
                return false;
            }
        }

        return true;
    }
}
