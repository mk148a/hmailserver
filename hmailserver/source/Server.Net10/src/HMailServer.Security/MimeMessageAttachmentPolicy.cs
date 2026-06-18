using HMailServer.Core.Abstractions;
using MimeKit;

namespace HMailServer.Security;

public sealed class MimeMessageAttachmentPolicy : IMessageAttachmentPolicy
{
    private readonly MessageAttachmentPolicyOptions _options;
    private readonly string[] _blockedWildcards;

    public MimeMessageAttachmentPolicy(MessageAttachmentPolicyOptions? options = null)
    {
        _options = options ?? new MessageAttachmentPolicyOptions();
        _blockedWildcards = _options.BlockedWildcards
            .Select(NormalizeWildcard)
            .Where(static wildcard => wildcard.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public ValueTask<MessageAttachmentPolicyResult> ApplyAsync(
        byte[] messageData,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messageData);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.Enabled || _blockedWildcards.Length == 0)
        {
            return ValueTask.FromResult(MessageAttachmentPolicyResult.Unchanged(messageData));
        }

        MimeMessage message;
        try
        {
            using var input = new MemoryStream(messageData, writable: false);
            message = MimeMessage.Load(input, cancellationToken);
        }
        catch (FormatException)
        {
            return ValueTask.FromResult(MessageAttachmentPolicyResult.Unchanged(messageData));
        }
        catch (InvalidOperationException)
        {
            return ValueTask.FromResult(MessageAttachmentPolicyResult.Unchanged(messageData));
        }

        var blockedFileNames = new List<string>();
        if (message.Body is not null)
        {
            message.Body = ReplaceBlockedAttachments(message.Body, blockedFileNames);
        }

        if (blockedFileNames.Count == 0)
        {
            return ValueTask.FromResult(MessageAttachmentPolicyResult.Unchanged(messageData));
        }

        using var output = new MemoryStream();
        message.WriteTo(output, cancellationToken);
        return ValueTask.FromResult(
            new MessageAttachmentPolicyResult(
                output.ToArray(),
                Modified: true,
                blockedFileNames));
    }

    private MimeEntity ReplaceBlockedAttachments(
        MimeEntity entity,
        List<string> blockedFileNames)
    {
        if (entity is Multipart multipart)
        {
            for (var index = 0; index < multipart.Count; index++)
            {
                multipart[index] = ReplaceBlockedAttachments(multipart[index], blockedFileNames);
            }

            return multipart;
        }

        if (entity is MessagePart messagePart && messagePart.Message?.Body is not null)
        {
            messagePart.Message.Body = ReplaceBlockedAttachments(messagePart.Message.Body, blockedFileNames);
            return messagePart;
        }

        if (entity is MimePart part)
        {
            var fileName = GetFileName(part);
            if (fileName.Length > 0 && IsBlocked(fileName))
            {
                blockedFileNames.Add(fileName);
                return CreateReplacementAttachment(fileName);
            }
        }

        return entity;
    }

    private MimeEntity CreateReplacementAttachment(string fileName)
    {
        var replacementFileName = fileName + ".txt";
        var text = _options.ReplacementTextTemplate.Replace("%MACRO_FILE%", fileName, StringComparison.Ordinal);
        var replacement = new TextPart("plain")
        {
            Text = text,
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            FileName = replacementFileName
        };
        replacement.ContentType.Name = replacementFileName;
        replacement.ContentDisposition.FileName = replacementFileName;
        return replacement;
    }

    private bool IsBlocked(string fileName) =>
        _blockedWildcards.Any(wildcard => WildcardMatch(wildcard, fileName));

    private static string GetFileName(MimePart part) =>
        part.FileName
        ?? part.ContentDisposition?.FileName
        ?? part.ContentType.Name
        ?? string.Empty;

    private static string NormalizeWildcard(string wildcard)
    {
        var normalized = wildcard.Trim();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        if (normalized[0] == '.')
        {
            return "*" + normalized;
        }

        if (!normalized.Contains('*', StringComparison.Ordinal)
            && !normalized.Contains('?', StringComparison.Ordinal)
            && !normalized.Contains('.', StringComparison.Ordinal))
        {
            return "*." + normalized;
        }

        return normalized;
    }

    private static bool WildcardMatch(string pattern, string value)
    {
        var patternIndex = 0;
        var valueIndex = 0;
        var starIndex = -1;
        var matchIndex = 0;

        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length
                && (pattern[patternIndex] == '?'
                    || char.ToUpperInvariant(pattern[patternIndex]) == char.ToUpperInvariant(value[valueIndex])))
            {
                patternIndex++;
                valueIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starIndex = patternIndex;
                matchIndex = valueIndex;
                patternIndex++;
            }
            else if (starIndex >= 0)
            {
                patternIndex = starIndex + 1;
                matchIndex++;
                valueIndex = matchIndex;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }
}
