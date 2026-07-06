using System.Net;
using System.Net.Sockets;

namespace HMailServer.Security;

public enum MailServerDnsStatus
{
    Success,
    NoData,
    NameError,
    TemporaryError
}

public sealed record MailServerDnsResponse<T>
{
    private MailServerDnsResponse(MailServerDnsStatus status, IReadOnlyList<T> records)
    {
        Status = status;
        Records = records;
    }

    public MailServerDnsStatus Status { get; }

    public IReadOnlyList<T> Records { get; }

    public static MailServerDnsResponse<T> Success(params T[] records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return new MailServerDnsResponse<T>(MailServerDnsStatus.Success, records.ToArray());
    }

    public static MailServerDnsResponse<T> NoData() =>
        new(MailServerDnsStatus.NoData, Array.Empty<T>());

    public static MailServerDnsResponse<T> NameError() =>
        new(MailServerDnsStatus.NameError, Array.Empty<T>());

    public static MailServerDnsResponse<T> TemporaryError() =>
        new(MailServerDnsStatus.TemporaryError, Array.Empty<T>());
}

public sealed record MailServerMxHost(string Exchange, ushort Preference);

public interface IMailServerDnsResolver
{
    ValueTask<MailServerDnsResponse<MailServerMxHost>> QueryMailServerMxAsync(
        string domain,
        CancellationToken cancellationToken);

    ValueTask<MailServerDnsResponse<string>> QueryMailServerCnameAsync(
        string domain,
        CancellationToken cancellationToken);

    ValueTask<MailServerDnsResponse<IPAddress>> QueryMailServerAddressesAsync(
        string domain,
        AddressFamily addressFamily,
        CancellationToken cancellationToken);
}
