using System.Globalization;
using System.Text;

namespace HMailServer.Protocols.Imap;

public static class ImapSearchResultFormatter
{
    public static string Format(IReadOnlyCollection<long> identifiers)
    {
        ArgumentNullException.ThrowIfNull(identifiers);

        var builder = new StringBuilder("* SEARCH");
        foreach (var identifier in identifiers)
        {
            builder.Append(' ')
                .Append(identifier.ToString(CultureInfo.InvariantCulture));
        }

        builder.Append("\r\n");
        return builder.ToString();
    }
}
