using System.Net;
using System.Net.Sockets;
using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed class SmtpReverseDnsChecker : ISmtpReverseDnsChecker
{
    private readonly IDnsReverseResolver _reverseResolver;
    private readonly IDnsAddressResolver _addressResolver;
    private readonly SmtpReverseDnsCheckOptions _options;

    public SmtpReverseDnsChecker(
        IDnsReverseResolver reverseResolver,
        IDnsAddressResolver addressResolver,
        SmtpReverseDnsCheckOptions? options = null)
    {
        _reverseResolver = reverseResolver;
        _addressResolver = addressResolver;
        _options = options ?? new SmtpReverseDnsCheckOptions();
    }

    public async ValueTask<SmtpReverseDnsResult> CheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled
            || (_options.SkipAuthenticated && request.IsAuthenticated)
            || !IPAddress.TryParse(request.ClientIPAddress, out var clientAddress))
        {
            return SmtpReverseDnsResult.Passed;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_options.Timeout > TimeSpan.Zero)
        {
            timeout.CancelAfter(_options.Timeout);
        }

        IReadOnlyList<string> hostNames;
        try
        {
            hostNames = await _reverseResolver
                .ResolveHostNamesAsync(clientAddress, timeout.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SmtpReverseDnsResult.Passed;
        }
        catch (SocketException ex) when (IsMissingPtr(ex))
        {
            hostNames = Array.Empty<string>();
        }
        catch (SocketException)
        {
            return SmtpReverseDnsResult.Passed;
        }
        catch
        {
            return SmtpReverseDnsResult.Passed;
        }

        hostNames = NormalizeHostNames(hostNames);
        if (hostNames.Count == 0)
        {
            return Reject(clientAddress, hostNames, "missing-ptr");
        }

        if (!_options.RequireForwardConfirmed)
        {
            return SmtpReverseDnsResult.Passed;
        }

        foreach (var hostName in hostNames)
        {
            IReadOnlyList<IPAddress> addresses;
            try
            {
                addresses = await _addressResolver
                    .ResolveAddressesAsync(hostName, timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return SmtpReverseDnsResult.Passed;
            }
            catch (SocketException)
            {
                continue;
            }
            catch
            {
                continue;
            }

            if (addresses.Any(clientAddress.Equals))
            {
                return SmtpReverseDnsResult.Passed;
            }
        }

        return Reject(clientAddress, hostNames, "forward-confirmation-failed");
    }

    private SmtpReverseDnsResult Reject(
        IPAddress clientAddress,
        IReadOnlyList<string> hostNames,
        string reason)
    {
        var clientIp = clientAddress.ToString();
        var hostList = hostNames.Count == 0
            ? string.Empty
            : string.Join(", ", hostNames);
        var response = _options.RejectionMessageTemplate
            .Replace("{ClientIP}", clientIp, StringComparison.Ordinal)
            .Replace("{HostNames}", hostList, StringComparison.Ordinal)
            .Replace("{Reason}", reason, StringComparison.Ordinal);
        response = response.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (response.Length == 0)
        {
            response = "554 Rejected by reverse DNS check";
        }

        if (!StartsWithSmtpReplyCode(response))
        {
            response = "554 " + response;
        }

        return SmtpReverseDnsResult.Reject(clientIp, hostNames, reason, response);
    }

    private static IReadOnlyList<string> NormalizeHostNames(IReadOnlyList<string> hostNames)
    {
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hostName in hostNames)
        {
            var value = hostName.Trim().TrimEnd('.').ToLowerInvariant();
            if (value.Length > 0)
            {
                normalized.Add(value);
            }
        }

        return normalized.ToArray();
    }

    private static bool IsMissingPtr(SocketException ex) =>
        ex.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData;

    private static bool StartsWithSmtpReplyCode(string value) =>
        value.Length >= 4
        && char.IsDigit(value[0])
        && char.IsDigit(value[1])
        && char.IsDigit(value[2])
        && value[3] == ' ';
}
