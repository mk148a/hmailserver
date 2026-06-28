using System.Text;

namespace HMailServer.Security;

public static class DkimMessageVerifier
{
    private const int MaxSignatureFields = 5;

    public static async ValueTask<DkimEvaluation> VerifyAsync(
        string message,
        IDkimTxtResolver resolver,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(resolver);

        SplitMessage(NormalizeLineEndings(message), out var headerBlock, out var body);
        return await VerifyAsync(headerBlock, body, resolver, cancellationToken)
            .ConfigureAwait(false);
    }

    public static async ValueTask<DkimEvaluation> VerifyAsync(
        string headerBlock,
        string body,
        IDkimTxtResolver resolver,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(headerBlock);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(resolver);

        var signatureFields = ExtractSignatureFields(headerBlock);
        if (signatureFields.Count == 0)
        {
            return new DkimEvaluation(
                DkimResult.Neutral,
                "DKIM verification neutral: no DKIM-Signature header fields were found.");
        }

        var result = new DkimEvaluation(
            DkimResult.Neutral,
            "DKIM verification neutral: no usable DKIM-Signature header fields were found.");
        foreach (var signatureHeaderValue in signatureFields)
        {
            result = await VerifySignatureAsync(
                headerBlock,
                body,
                signatureHeaderValue,
                resolver,
                cancellationToken).ConfigureAwait(false);
            if (result.Result == DkimResult.Pass)
            {
                return result;
            }
        }

        return result;
    }

    private static async ValueTask<DkimEvaluation> VerifySignatureAsync(
        string headerBlock,
        string body,
        string signatureHeaderValue,
        IDkimTxtResolver resolver,
        CancellationToken cancellationToken)
    {
        if (!DkimSignatureParser.TryParse(
                signatureHeaderValue,
                out var signature,
                out var diagnostic)
            || signature is null)
        {
            return new DkimEvaluation(
                DkimResult.Neutral,
                "DKIM signature ignored: " + diagnostic);
        }

        return await DkimSignatureVerifier.VerifyAsync(
            headerBlock,
            body,
            signatureHeaderValue,
            signature,
            resolver,
            cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> ExtractSignatureFields(string headerBlock)
    {
        var result = new List<string>();
        var current = new StringBuilder();

        foreach (var line in headerBlock.Split("\r\n", StringSplitOptions.None))
        {
            if (line.Length == 0)
            {
                continue;
            }

            if ((line[0] == ' ' || line[0] == '\t') && current.Length > 0)
            {
                current.Append("\r\n").Append(line);
                continue;
            }

            AddCurrentField(result, current);
            if (result.Count >= MaxSignatureFields)
            {
                return result;
            }

            current.Clear();
            current.Append(line);
        }

        AddCurrentField(result, current);
        return result;
    }

    private static void AddCurrentField(
        ICollection<string> fields,
        StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        var rawLine = current.ToString();
        var colonIndex = rawLine.IndexOf(':');
        if (colonIndex <= 0)
        {
            return;
        }

        var name = rawLine[..colonIndex].Trim();
        if (name.Equals("DKIM-Signature", StringComparison.OrdinalIgnoreCase))
        {
            fields.Add(rawLine[(colonIndex + 1)..].TrimStart(' ', '\t'));
        }
    }

    private static void SplitMessage(
        string message,
        out string headerBlock,
        out string body)
    {
        var separatorIndex = message.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            headerBlock = message;
            body = string.Empty;
            return;
        }

        headerBlock = message[..separatorIndex];
        body = message[(separatorIndex + 4)..];
    }

    private static string NormalizeLineEndings(string value)
    {
        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return normalized.Replace("\n", "\r\n", StringComparison.Ordinal);
    }
}
