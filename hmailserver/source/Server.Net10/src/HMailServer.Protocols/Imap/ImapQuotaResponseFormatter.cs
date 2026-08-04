using System.Globalization;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public static class ImapQuotaResponseFormatter
{
    public static string FormatQuota(ImapQuota quota)
    {
        ArgumentNullException.ThrowIfNull(quota);

        var root = Quote(quota.RootName);
        if (quota.LimitKilobytes is null)
        {
            return "* QUOTA " + root + " (STORAGE)\r\n";
        }

        return "* QUOTA " +
            root +
            " (STORAGE " +
            quota.UsedKilobytes.ToString(CultureInfo.InvariantCulture) +
            " " +
            quota.LimitKilobytes.Value.ToString(CultureInfo.InvariantCulture) +
            ")\r\n";
    }

    public static string FormatQuotaRoot(ImapQuotaRootResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Quota is null)
        {
            throw new InvalidOperationException("QUOTAROOT response requires quota data.");
        }

        var quota = result.Quota.LimitKilobytes is null
            ? "* QUOTA " + Quote(result.Quota.RootName) + " ()\r\n"
            : FormatQuota(result.Quota);

        var mailbox = result.MailboxWasQuoted ? Quote(result.MailboxName) : result.MailboxName;
        return "* QUOTAROOT " + mailbox + " " + Quote(result.Quota.RootName) + "\r\n" + quota;
    }

    private static string Quote(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return "\"" + escaped + "\"";
    }
}
