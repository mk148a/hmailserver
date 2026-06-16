using System.Globalization;

namespace HMailServer.Protocols.Imap;

public static class ImapSortResultFormatter
{
    public static string Format(IReadOnlyList<long> identifiers) =>
        identifiers.Count == 0
            ? "* SORT\r\n"
            : "* SORT " + string.Join(' ', identifiers.Select(static identifier => identifier.ToString(CultureInfo.InvariantCulture))) + "\r\n";
}
