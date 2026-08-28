using System.Net;
using System.Text;
using MimeKit;

namespace HMailServer.Storage.SqlServer;

public sealed record MessageSearchText(
    string HeaderText,
    string BodyText,
    string CombinedText,
    string SubjectText);

public static class MimeMessageSearchTextExtractor
{
    public static MessageSearchText Extract(
        MimeMessage message,
        MessageFileSearchDocumentSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(options);

        var headerText = Truncate(BuildHeaderText(message), options.MaxHeaderChars);
        var bodyText = BuildBodyText(message, options.MaxBodyChars);
        var combinedText = Truncate(headerText + Environment.NewLine + bodyText, options.MaxCombinedChars);

        return new MessageSearchText(
            headerText,
            bodyText,
            combinedText,
            message.Subject ?? string.Empty);
    }

    private static string BuildBodyText(MimeMessage message, int maxBodyChars)
    {
        var builder = new StringBuilder(Math.Min(maxBodyChars, 32 * 1024));
        AppendLimited(builder, message.TextBody, maxBodyChars);

        if (!string.IsNullOrWhiteSpace(message.HtmlBody) && builder.Length < maxBodyChars)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            AppendLimited(builder, HtmlToText(message.HtmlBody), maxBodyChars);
        }

        return builder.ToString();
    }

    private static string BuildHeaderText(MimeMessage message)
    {
        var builder = new StringBuilder();
        foreach (var header in message.Headers)
        {
            builder.Append(header.Field)
                .Append(": ")
                .AppendLine(header.Value);
        }

        return builder.ToString();
    }

    private static void AppendLimited(StringBuilder builder, string? value, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value) || builder.Length >= maxChars)
        {
            return;
        }

        var remaining = maxChars - builder.Length;
        builder.Append(value.AsSpan(0, Math.Min(value.Length, remaining)));
    }

    private static string Truncate(string value, int maxChars)
    {
        return value.Length <= maxChars ? value : value[..maxChars];
    }

    private static string HtmlToText(string html)
    {
        var builder = new StringBuilder(html.Length);
        var inTag = false;
        var lastWasWhitespace = false;

        foreach (var character in html)
        {
            switch (character)
            {
                case '<':
                    inTag = true;
                    if (!lastWasWhitespace)
                    {
                        builder.Append(' ');
                        lastWasWhitespace = true;
                    }
                    break;
                case '>':
                    inTag = false;
                    break;
                default:
                    if (inTag)
                    {
                        break;
                    }

                    if (char.IsWhiteSpace(character))
                    {
                        if (!lastWasWhitespace)
                        {
                            builder.Append(' ');
                            lastWasWhitespace = true;
                        }
                    }
                    else
                    {
                        builder.Append(character);
                        lastWasWhitespace = false;
                    }

                    break;
            }
        }

        return WebUtility.HtmlDecode(builder.ToString()).Trim();
    }
}
