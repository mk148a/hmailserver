namespace HMailServer.Security;

public static class SpamAssassinResponseValidator
{
    public static bool TryReadContentLength(ReadOnlySpan<char> header, out int contentLength)
    {
        contentLength = 0;
        var lines = header.ToString().Split("\r\n", StringSplitOptions.None);
        if (lines.Length < 2 || !StringComparer.Ordinal.Equals(lines[0], "SPAMD/1.1 0 EX_OK"))
        {
            return false;
        }

        const string prefix = "Content-length:";
        if (!lines[1].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = lines[1][prefix.Length..].Trim();
        return int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out contentLength)
            && contentLength > 0;
    }
}
