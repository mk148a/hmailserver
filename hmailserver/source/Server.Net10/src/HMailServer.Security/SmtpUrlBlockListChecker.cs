using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using HMailServer.Core.Abstractions;
using MimeKit;

namespace HMailServer.Security;

public sealed class SmtpUrlBlockListChecker : ISmtpUrlBlockListChecker
{
    private static readonly Regex UrlRegex = new(
        @"\b(?:(?:https?://)|(?:www\.))[^\s<>()""']+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IDnsAddressResolver _resolver;
    private readonly SmtpUrlBlockListOptions _options;
    private readonly string[] _zones;

    public SmtpUrlBlockListChecker(
        IDnsAddressResolver resolver,
        SmtpUrlBlockListOptions? options = null)
    {
        _resolver = resolver;
        _options = options ?? new SmtpUrlBlockListOptions();
        _zones = _options.Zones
            .Select(NormalizeZone)
            .Where(static zone => zone.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async ValueTask<SmtpUrlBlockListResult> CheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled
            || _zones.Length == 0
            || !request.EnableSpamScan
            || (_options.SkipAuthenticated && request.IsAuthenticated))
        {
            return SmtpUrlBlockListResult.NotListed;
        }

        var hosts = ExtractHosts(request.MessageData);
        if (hosts.Count == 0)
        {
            return SmtpUrlBlockListResult.NotListed;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_options.Timeout > TimeSpan.Zero)
        {
            timeout.CancelAfter(_options.Timeout);
        }

        foreach (var host in hosts)
        {
            foreach (var candidateDomain in BuildCandidateDomains(host))
            {
                foreach (var zone in _zones)
                {
                    var queryHost = candidateDomain + "." + zone;
                    IReadOnlyList<IPAddress> responseAddresses;
                    try
                    {
                        responseAddresses = await _resolver
                            .ResolveAddressesAsync(queryHost, timeout.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        return SmtpUrlBlockListResult.NotListed;
                    }
                    catch (SocketException)
                    {
                        continue;
                    }
                    catch
                    {
                        continue;
                    }

                    if (responseAddresses.Count > 0)
                    {
                        var responseAddress = responseAddresses[0].ToString();
                        return SmtpUrlBlockListResult.Blocked(
                            zone,
                            host,
                            queryHost,
                            responseAddress,
                            BuildFailureResponse(zone, host, queryHost, responseAddress));
                    }
                }
            }
        }

        return SmtpUrlBlockListResult.NotListed;
    }

    private IReadOnlyList<string> ExtractHosts(byte[] messageData)
    {
        ArgumentNullException.ThrowIfNull(messageData);

        var texts = new List<string>();
        if (!TryExtractMimeTexts(messageData, texts))
        {
            texts.Add(Encoding.Latin1.GetString(messageData));
        }

        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var text in texts)
        {
            foreach (Match match in UrlRegex.Matches(text))
            {
                var host = TryGetHost(match.Value);
                if (host.Length == 0)
                {
                    continue;
                }

                hosts.Add(host);
                if (hosts.Count >= Math.Max(1, _options.MaxHosts))
                {
                    return hosts.ToArray();
                }
            }
        }

        return hosts.ToArray();
    }

    private static bool TryExtractMimeTexts(
        byte[] messageData,
        List<string> texts)
    {
        try
        {
            using var input = new MemoryStream(messageData, writable: false);
            var message = MimeMessage.Load(input);
            if (message.Body is not null)
            {
                AddTextParts(message.Body, texts);
            }

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void AddTextParts(
        MimeEntity entity,
        List<string> texts)
    {
        if (entity is Multipart multipart)
        {
            foreach (var child in multipart)
            {
                AddTextParts(child, texts);
            }

            return;
        }

        if (entity is MessagePart messagePart && messagePart.Message?.Body is not null)
        {
            AddTextParts(messagePart.Message.Body, texts);
            return;
        }

        if (entity is TextPart textPart
            && (textPart.IsPlain || textPart.IsHtml)
            && !string.IsNullOrEmpty(textPart.Text))
        {
            texts.Add(textPart.Text);
        }
    }

    private IEnumerable<string> BuildCandidateDomains(string host)
    {
        var yielded = 0;
        var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < labels.Length - 1; index++)
        {
            if (yielded >= Math.Max(1, _options.MaxCandidateDomainsPerHost))
            {
                yield break;
            }

            var candidate = string.Join('.', labels.Skip(index));
            if (candidate.Contains('.', StringComparison.Ordinal))
            {
                yielded++;
                yield return candidate;
            }
        }
    }

    private string BuildFailureResponse(
        string listHost,
        string matchedHost,
        string queryHost,
        string responseAddress)
    {
        var message = _options.RejectionMessageTemplate
            .Replace("{ListHost}", listHost, StringComparison.Ordinal)
            .Replace("{MatchedHost}", matchedHost, StringComparison.Ordinal)
            .Replace("{QueryHost}", queryHost, StringComparison.Ordinal)
            .Replace("{ResponseAddress}", responseAddress, StringComparison.Ordinal);
        message = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (message.Length == 0)
        {
            return "554 Rejected by URL blocklist";
        }

        return StartsWithSmtpReplyCode(message)
            ? message
            : "554 " + message;
    }

    private static string TryGetHost(string value)
    {
        var url = value.TrimEnd('.', ',', ';', ':', '!', '?', ']', '}');
        if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            url = "http://" + url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return string.Empty;
        }

        var host = uri.Host.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.Length == 0
            || !host.Contains('.', StringComparison.Ordinal)
            || IPAddress.TryParse(host, out _))
        {
            return string.Empty;
        }

        try
        {
            host = new IdnMapping().GetAscii(host);
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }

        return host;
    }

    private static string NormalizeZone(string zone) =>
        zone.Trim().TrimEnd('.');

    private static bool StartsWithSmtpReplyCode(string value) =>
        value.Length >= 4
        && char.IsDigit(value[0])
        && char.IsDigit(value[1])
        && char.IsDigit(value[2])
        && value[3] == ' ';
}
