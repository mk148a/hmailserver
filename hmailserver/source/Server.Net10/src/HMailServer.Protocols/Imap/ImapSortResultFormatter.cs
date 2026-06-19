using System.Globalization;
using System.Text;

namespace HMailServer.Protocols.Imap;

public static class ImapSortResultFormatter
{
    public static string Format(IReadOnlyCollection<long> identifiers)
    {
        ArgumentNullException.ThrowIfNull(identifiers);

        var builder = new StringBuilder("* SORT");
        foreach (var identifier in identifiers)
        {
            builder.Append(' ')
                .Append(identifier.ToString(CultureInfo.InvariantCulture));
        }

        builder.Append("\r\n");
        return builder.ToString();
    }
}
