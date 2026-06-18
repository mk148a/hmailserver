using System.Globalization;
using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed class MessageSpamPolicy : IMessageSpamPolicy
{
    private const string SpamHeader = "X-hMailServer-Spam";
    private const string ReasonHeaderPrefix = "X-hMailServer-Reason-";
    private const string ReasonScoreHeader = "X-hMailServer-Reason-Score";
    private static readonly Encoding HeaderEncoding = Encoding.Latin1;
    private readonly MessageSpamPolicyOptions _options;

    public MessageSpamPolicy(MessageSpamPolicyOptions? options = null)
    {
        _options = options ?? new MessageSpamPolicyOptions();
    }

    public MessageSpamPolicyResult Apply(
        byte[] messageData,
        MessageSpamScanResult scanResult)
    {
        ArgumentNullException.ThrowIfNull(messageData);
        ArgumentNullException.ThrowIfNull(scanResult);

        if (!scanResult.Succeeded || !IsEnabled(_options))
        {
            return new MessageSpamPolicyResult(messageData, MarkAsSpam: false);
        }

        if (_options.SpamDeleteThreshold > 0 && scanResult.Score >= _options.SpamDeleteThreshold)
        {
            return new MessageSpamPolicyResult(
                messageData,
                MarkAsSpam: false,
                RejectMessage: true,
                FailureResponse: BuildSpamRejectedResponse(scanResult));
        }

        var markAsSpam = scanResult.IsSpam
            || (_options.SpamMarkThreshold > 0 && scanResult.Score >= _options.SpamMarkThreshold);
        var editor = MessageHeaderEditor.Parse(messageData);
        var changed = false;

        if (markAsSpam)
        {
            if (_options.AddSpamHeader)
            {
                editor.SetHeader(SpamHeader, "YES", SanitizeHeaderValue);
                changed = true;
            }

            if (_options.PrependSubject && !string.IsNullOrWhiteSpace(_options.SubjectPrefix))
            {
                var prefix = SanitizeHeaderValue(_options.SubjectPrefix);
                var subject = editor.GetHeader("Subject");
                if (!subject.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    editor.SetHeader("Subject", subject.Length == 0 ? prefix : prefix + " " + subject, SanitizeHeaderValue);
                    changed = true;
                }
            }
        }

        if (_options.AddReasonHeaders)
        {
            editor.RemoveHeadersByPrefix(ReasonHeaderPrefix);
            if (markAsSpam)
            {
                var details = string.IsNullOrWhiteSpace(scanResult.Details)
                    ? "Tagged as Spam"
                    : scanResult.Details;
                editor.SetHeader(
                    ReasonHeaderPrefix + "1",
                    details + " - (Score: " + scanResult.Score.ToString(CultureInfo.InvariantCulture) + ")",
                    SanitizeHeaderValue);
            }

            editor.SetHeader(
                ReasonScoreHeader,
                scanResult.Score.ToString(CultureInfo.InvariantCulture),
                SanitizeHeaderValue);
            changed = true;
        }

        return new MessageSpamPolicyResult(changed ? editor.ToArray() : messageData, markAsSpam);
    }

    private static bool IsEnabled(MessageSpamPolicyOptions options) =>
        options.AddSpamHeader
        || options.AddReasonHeaders
        || options.PrependSubject
        || options.SpamMarkThreshold > 0
        || options.SpamDeleteThreshold > 0;

    private string BuildSpamRejectedResponse(MessageSpamScanResult scanResult)
    {
        var details = string.IsNullOrWhiteSpace(scanResult.Details)
            ? "Message rejected as spam"
            : scanResult.Details;
        return "554 " + SanitizeHeaderValue(details);
    }

    private string SanitizeHeaderValue(string value)
    {
        var sanitized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (_options.MaxHeaderValueLength <= 0 || sanitized.Length <= _options.MaxHeaderValueLength)
        {
            return sanitized;
        }

        return sanitized[.._options.MaxHeaderValueLength];
    }

    private sealed class MessageHeaderEditor
    {
        private readonly List<string> _headers;
        private readonly byte[] _body;

        private MessageHeaderEditor(
            List<string> headers,
            byte[] body)
        {
            _headers = headers;
            _body = body;
        }

        public static MessageHeaderEditor Parse(byte[] messageData)
        {
            var (headerLength, separatorLength) = FindHeaderEnd(messageData);
            var headerText = HeaderEncoding.GetString(messageData, 0, headerLength);
            var bodyStart = Math.Min(messageData.Length, headerLength + separatorLength);
            var body = messageData.AsSpan(bodyStart).ToArray();

            return new MessageHeaderEditor(ParseHeaderBlocks(headerText), body);
        }

        public string GetHeader(string name)
        {
            foreach (var header in _headers)
            {
                if (HeaderNameEquals(header, name))
                {
                    return GetHeaderValue(header);
                }
            }

            return string.Empty;
        }

        public void SetHeader(
            string name,
            string value,
            Func<string, string> sanitize)
        {
            RemoveHeaders(name);
            _headers.Add(name + ": " + sanitize(value));
        }

        public void RemoveHeadersByPrefix(string prefix)
        {
            _headers.RemoveAll(header =>
                GetHeaderName(header).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        public byte[] ToArray()
        {
            var headerBytes = HeaderEncoding.GetBytes(string.Join("\r\n", _headers) + "\r\n\r\n");
            var output = new byte[headerBytes.Length + _body.Length];
            Buffer.BlockCopy(headerBytes, 0, output, 0, headerBytes.Length);
            Buffer.BlockCopy(_body, 0, output, headerBytes.Length, _body.Length);
            return output;
        }

        private void RemoveHeaders(string name)
        {
            _headers.RemoveAll(header => HeaderNameEquals(header, name));
        }

        private static (int HeaderLength, int SeparatorLength) FindHeaderEnd(byte[] messageData)
        {
            for (var index = 0; index <= messageData.Length - 4; index++)
            {
                if (messageData[index] == '\r'
                    && messageData[index + 1] == '\n'
                    && messageData[index + 2] == '\r'
                    && messageData[index + 3] == '\n')
                {
                    return (index, 4);
                }
            }

            for (var index = 0; index <= messageData.Length - 2; index++)
            {
                if (messageData[index] == '\n' && messageData[index + 1] == '\n')
                {
                    return (index, 2);
                }
            }

            return (messageData.Length, 0);
        }

        private static List<string> ParseHeaderBlocks(string headerText)
        {
            var headers = new List<string>();
            var lines = headerText
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');

            foreach (var line in lines)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                if ((line[0] == ' ' || line[0] == '\t') && headers.Count > 0)
                {
                    headers[^1] += "\r\n" + line;
                    continue;
                }

                headers.Add(line);
            }

            return headers;
        }

        private static bool HeaderNameEquals(string header, string name) =>
            string.Equals(GetHeaderName(header), name, StringComparison.OrdinalIgnoreCase);

        private static string GetHeaderName(string header)
        {
            var separatorIndex = header.IndexOf(':', StringComparison.Ordinal);
            return separatorIndex < 0 ? string.Empty : header[..separatorIndex].Trim();
        }

        private static string GetHeaderValue(string header)
        {
            var lines = header
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');
            if (lines.Length == 0)
            {
                return string.Empty;
            }

            var separatorIndex = lines[0].IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                return string.Empty;
            }

            var value = lines[0][(separatorIndex + 1)..].TrimStart();
            for (var index = 1; index < lines.Length; index++)
            {
                value += " " + lines[index].Trim();
            }

            return value;
        }
    }
}
