using System.Net;
using System.Net.Sockets;

namespace HMailServer.Security;

public enum SpfDnsStatus
{
    Success,
    NoData,
    NameError,
    TemporaryError
}

public sealed record SpfDnsResponse<T>
{
    private SpfDnsResponse(SpfDnsStatus status, IReadOnlyList<T> records)
    {
        Status = status;
        Records = records;
    }

    public SpfDnsStatus Status { get; }

    public IReadOnlyList<T> Records { get; }

    public static SpfDnsResponse<T> Success(params T[] records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return new SpfDnsResponse<T>(SpfDnsStatus.Success, records.ToArray());
    }

    public static SpfDnsResponse<T> NoData() =>
        new(SpfDnsStatus.NoData, Array.Empty<T>());

    public static SpfDnsResponse<T> NameError() =>
        new(SpfDnsStatus.NameError, Array.Empty<T>());

    public static SpfDnsResponse<T> TemporaryError() =>
        new(SpfDnsStatus.TemporaryError, Array.Empty<T>());
}

public sealed record SpfMxHost(string Exchange, ushort Preference);

public interface ISpfDnsResolver
{
    ValueTask<SpfDnsResponse<string>> QueryTxtAsync(
        string domain,
        CancellationToken cancellationToken);

    ValueTask<SpfDnsResponse<IPAddress>> QueryAddressesAsync(
        string domain,
        AddressFamily addressFamily,
        CancellationToken cancellationToken);

    ValueTask<SpfDnsResponse<SpfMxHost>> QueryMxAsync(
        string domain,
        CancellationToken cancellationToken);

    ValueTask<SpfDnsResponse<string>> QueryPtrAsync(
        IPAddress address,
        CancellationToken cancellationToken);
}
