using System.Globalization;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapQuotaCommandHandler
{
    private readonly IImapQuotaStore _quotaStore;

    public ImapQuotaCommandHandler(IImapQuotaStore quotaStore)
    {
        _quotaStore = quotaStore;
    }

    public async ValueTask<string> HandleAsync(
        int requesterAccountId,
        string tag,
        string command,
        string arguments,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requesterAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(arguments);

        try
        {
            return command.ToUpperInvariant() switch
            {
                "GETQUOTA" => await HandleGetQuotaAsync(requesterAccountId, tag, arguments, cancellationToken).ConfigureAwait(false),
                "GETQUOTAROOT" => await HandleGetQuotaRootAsync(requesterAccountId, tag, arguments, cancellationToken).ConfigureAwait(false),
                "SETQUOTA" => await HandleSetQuotaAsync(requesterAccountId, tag, arguments, cancellationToken).ConfigureAwait(false),
                _ => TaggedBad(tag, "Unsupported QUOTA command")
            };
        }
        catch (ImapSearchParseException ex)
        {
            return TaggedBad(tag, ex.Message);
        }
    }

    private async ValueTask<string> HandleGetQuotaAsync(
        int requesterAccountId,
        string tag,
        string arguments,
        CancellationToken cancellationToken)
    {
        var parsed = ImapCommandArguments.Parse(arguments);
        if (parsed.Count != 1)
        {
            return TaggedBad(tag, "GETQUOTA requires one quota root");
        }

        var result = await _quotaStore.GetQuotaAsync(requesterAccountId, parsed[0], cancellationToken).ConfigureAwait(false);
        if (result.Status != ImapQuotaCommandStatus.Success || result.Quota is null)
        {
            return FormatFailure(tag, result.Status, "GETQUOTA");
        }

        return ImapQuotaResponseFormatter.FormatQuota(result.Quota) + $"{SanitizeAtom(tag)} OK GETQUOTA completed\r\n";
    }

    private async ValueTask<string> HandleGetQuotaRootAsync(
        int requesterAccountId,
        string tag,
        string arguments,
        CancellationToken cancellationToken)
    {
        var parsed = ImapCommandArguments.Parse(arguments);
        if (parsed.Count != 1)
        {
            return TaggedBad(tag, "GETQUOTAROOT requires one mailbox name");
        }

        var result = await _quotaStore.GetQuotaRootAsync(requesterAccountId, parsed[0], cancellationToken).ConfigureAwait(false);
        if (result.Status != ImapQuotaCommandStatus.Success || result.Quota is null)
        {
            return FormatFailure(tag, result.Status, "GETQUOTAROOT");
        }

        return ImapQuotaResponseFormatter.FormatQuotaRoot(result) + $"{SanitizeAtom(tag)} OK GETQUOTAROOT completed\r\n";
    }

    private async ValueTask<string> HandleSetQuotaAsync(
        int requesterAccountId,
        string tag,
        string arguments,
        CancellationToken cancellationToken)
    {
        var (quotaRoot, limitKilobytes) = ParseSetQuotaArguments(arguments);
        var result = await _quotaStore.SetQuotaAsync(requesterAccountId, quotaRoot, limitKilobytes, cancellationToken).ConfigureAwait(false);
        return result.Status == ImapQuotaCommandStatus.Success
            ? $"{SanitizeAtom(tag)} OK SETQUOTA completed\r\n"
            : FormatFailure(tag, result.Status, "SETQUOTA");
    }

    private static (string QuotaRoot, long LimitKilobytes) ParseSetQuotaArguments(string arguments)
    {
        var index = 0;
        var quotaRoot = ReadAString(arguments, ref index);
        SkipWhitespace(arguments, ref index);
        if (index >= arguments.Length || arguments[index] != '(')
        {
            throw new ImapSearchParseException("SETQUOTA requires a parenthesized resource list.");
        }

        var closeIndex = arguments.IndexOf(')', index + 1);
        if (closeIndex < 0)
        {
            throw new ImapSearchParseException("SETQUOTA resource list is not terminated.");
        }

        var resourceArguments = ImapCommandArguments.Parse(arguments.Substring(index + 1, closeIndex - index - 1));
        if (resourceArguments.Count != 2 ||
            !resourceArguments[0].Equals("STORAGE", StringComparison.OrdinalIgnoreCase) ||
            !long.TryParse(resourceArguments[1], NumberStyles.None, CultureInfo.InvariantCulture, out var limitKilobytes) ||
            limitKilobytes < 0)
        {
            throw new ImapSearchParseException("SETQUOTA supports only STORAGE with a non-negative limit.");
        }

        var trailingIndex = closeIndex + 1;
        SkipWhitespace(arguments, ref trailingIndex);
        if (trailingIndex != arguments.Length)
        {
            throw new ImapSearchParseException("SETQUOTA has unexpected trailing arguments.");
        }

        return (quotaRoot, limitKilobytes);
    }

    private static string ReadAString(string value, ref int index)
    {
        SkipWhitespace(value, ref index);
        if (index >= value.Length)
        {
            throw new ImapSearchParseException("Missing quota root.");
        }

        return value[index] == '"'
            ? ReadQuoted(value, ref index)
            : ReadAtom(value, ref index);
    }

    private static string ReadQuoted(string value, ref int index)
    {
        index++;
        var builder = new System.Text.StringBuilder();
        while (index < value.Length)
        {
            var current = value[index++];
            if (current == '"')
            {
                return builder.ToString();
            }

            if (current == '\\')
            {
                if (index >= value.Length)
                {
                    throw new ImapSearchParseException("Quoted string ends with an escape character.");
                }

                builder.Append(value[index++]);
                continue;
            }

            builder.Append(current);
        }

        throw new ImapSearchParseException("Quoted string is not terminated.");
    }

    private static string ReadAtom(string value, ref int index)
    {
        var start = index;
        while (index < value.Length && !char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        return value[start..index];
    }

    private static void SkipWhitespace(string value, ref int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }
    }

    private static string FormatFailure(string tag, ImapQuotaCommandStatus status, string command) =>
        status switch
        {
            ImapQuotaCommandStatus.QuotaDisabled => TaggedNo(tag, "IMAP QUOTA is not enabled."),
            ImapQuotaCommandStatus.AccountNotFound => TaggedNo(tag, "Account could not be found."),
            ImapQuotaCommandStatus.QuotaRootNotFound => TaggedNo(tag, "Quota root could not be found."),
            ImapQuotaCommandStatus.PermissionDenied => TaggedNo(tag, $"{command} permission denied"),
            _ => TaggedNo(tag, $"{command} failed")
        };

    private static string TaggedBad(string tag, string response) =>
        $"{SanitizeAtom(tag)} BAD {SanitizeResponseText(response)}\r\n";

    private static string TaggedNo(string tag, string response) =>
        $"{SanitizeAtom(tag)} NO {SanitizeResponseText(response)}\r\n";

    private static string SanitizeAtom(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

    private static string SanitizeResponseText(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
