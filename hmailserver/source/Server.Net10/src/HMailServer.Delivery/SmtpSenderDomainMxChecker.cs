using System.Globalization;
using System.Net;
using System.Net.Sockets;
using HMailServer.Core.Abstractions;

namespace HMailServer.Delivery;

public sealed class SmtpSenderDomainMxChecker : ISmtpSenderDomainMxChecker
{
    private readonly IDnsMxResolver _mxResolver;
    private readonly SmtpSenderDomainMxCheckOptions _options;

    public SmtpSenderDomainMxChecker(
        IDnsMxResolver mxResolver,
        SmtpSenderDomainMxCheckOptions? options = null)
    {
        _mxResolver = mxResolver;
        _options = options ?? new SmtpSenderDomainMxCheckOptions();
    }

    public async ValueTask<SmtpSenderDomainMxResult> CheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled || (_options.SkipAuthenticated && request.IsAuthenticated))
        {
            return SmtpSenderDomainMxResult.Passed;
        }

        var senderDomain = ExtractSenderDomain(request.MailFrom);
        if (senderDomain.Length == 0)
        {
            return SmtpSenderDomainMxResult.Passed;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_options.Timeout > TimeSpan.Zero)
        {
            timeout.CancelAfter(_options.Timeout);
        }

        IReadOnlyList<DnsMxRecord> records;
        try
        {
            records = await _mxResolver
                .ResolveMxAsync(senderDomain, timeout.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SmtpSenderDomainMxResult.Passed;
        }
        catch (SocketException)
        {
            return SmtpSenderDomainMxResult.Passed;
        }
        catch (IOException)
        {
            return SmtpSenderDomainMxResult.Passed;
        }
        catch
        {
            return SmtpSenderDomainMxResult.Passed;
        }

        return records.Count > 0
            ? SmtpSenderDomainMxResult.Passed
            : Reject(senderDomain, "missing-mx");
    }

    private SmtpSenderDomainMxResult Reject(
        string senderDomain,
        string reason)
    {
        var response = _options.RejectionMessageTemplate
            .Replace("{SenderDomain}", senderDomain, StringComparison.Ordinal)
            .Replace("{Reason}", reason, StringComparison.Ordinal);
        response = response.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (response.Length == 0)
        {
            response = "554 Sender domain does not have any MX records";
        }

        if (!StartsWithSmtpReplyCode(response))
        {
            response = "554 " + response;
        }

        return SmtpSenderDomainMxResult.Reject(senderDomain, reason, response);
    }

    private static string ExtractSenderDomain(string mailFrom)
    {
        var sender = mailFrom.Trim();
        if (sender.Length == 0 || sender == "<>")
        {
            return string.Empty;
        }

        if (sender.Length >= 2 && sender[0] == '<' && sender[^1] == '>')
        {
            sender = sender[1..^1].Trim();
        }

        var atIndex = sender.LastIndexOf('@');
        if (atIndex < 0 || atIndex == sender.Length - 1)
        {
            return string.Empty;
        }

        var domain = sender[(atIndex + 1)..].Trim().TrimEnd('.');
        if (domain.Length == 0 || IsDomainLiteral(domain) || IPAddress.TryParse(domain, out _))
        {
            return string.Empty;
        }

        try
        {
            domain = new IdnMapping().GetAscii(domain).TrimEnd('.');
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }

        if (domain.Length is 0 or > 253)
        {
            return string.Empty;
        }

        var labels = domain.Split('.');
        if (labels.Any(static label => label.Length is 0 or > 63))
        {
            return string.Empty;
        }

        return domain.ToLowerInvariant();
    }

    private static bool IsDomainLiteral(string domain)
    {
        if (domain.Length < 3 || domain[0] != '[' || domain[^1] != ']')
        {
            return false;
        }

        var literal = domain[1..^1];
        if (literal.StartsWith("IPv6:", StringComparison.OrdinalIgnoreCase))
        {
            literal = literal["IPv6:".Length..];
        }

        return IPAddress.TryParse(literal, out _);
    }

    private static bool StartsWithSmtpReplyCode(string value) =>
        value.Length >= 4
        && char.IsDigit(value[0])
        && char.IsDigit(value[1])
        && char.IsDigit(value[2])
        && value[3] == ' ';
}
