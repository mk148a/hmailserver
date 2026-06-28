using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace HMailServer.Security;

public sealed class SystemSpfDnsResolver : ISpfDnsResolver
{
    private const int DnsPort = 53;
    private const ushort QueryFlags = 0x0100;
    private const ushort TypeA = 1;
    private const ushort TypePtr = 12;
    private const ushort TypeMx = 15;
    private const ushort TypeTxt = 16;
    private const ushort TypeAaaa = 28;
    private const ushort ClassInternet = 1;
    private const ushort ResponseCodeMask = 0x000F;
    private const ushort ResponseCodeNoError = 0;
    private const ushort ResponseCodeNameError = 3;
    private const ushort TruncatedFlag = 0x0200;
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(5);

    private readonly IReadOnlyList<IPEndPoint> _nameServers;
    private readonly DnsExchangeAsync _exchangeAsync;

    public SystemSpfDnsResolver()
        : this(GetSystemNameServerEndpoints(), ExchangeUdpAsync)
    {
    }

    public SystemSpfDnsResolver(IReadOnlyList<IPAddress> nameServers)
        : this(
            nameServers
                .Select(static address => new IPEndPoint(address, DnsPort))
                .ToArray(),
            ExchangeUdpAsync)
    {
    }

    internal SystemSpfDnsResolver(
        IReadOnlyList<IPEndPoint> nameServers,
        DnsExchangeAsync exchangeAsync)
    {
        _nameServers = nameServers;
        _exchangeAsync = exchangeAsync;
    }

    internal delegate ValueTask<byte[]> DnsExchangeAsync(
        IPEndPoint server,
        byte[] query,
        CancellationToken cancellationToken);

    public ValueTask<SpfDnsResponse<string>> QueryTxtAsync(
        string domain,
        CancellationToken cancellationToken) =>
        QueryAsync(domain, TypeTxt, ReadTxtRecord, cancellationToken);

    public ValueTask<SpfDnsResponse<IPAddress>> QueryAddressesAsync(
        string domain,
        AddressFamily addressFamily,
        CancellationToken cancellationToken)
    {
        var type = addressFamily switch
        {
            AddressFamily.InterNetwork => TypeA,
            AddressFamily.InterNetworkV6 => TypeAaaa,
            _ => (ushort)0
        };
        return type == 0
            ? ValueTask.FromResult(SpfDnsResponse<IPAddress>.NoData())
            : QueryAsync(domain, type, ReadAddressRecord, cancellationToken);
    }

    public async ValueTask<SpfDnsResponse<SpfMxHost>> QueryMxAsync(
        string domain,
        CancellationToken cancellationToken)
    {
        var response = await QueryAsync(domain, TypeMx, ReadMxRecord, cancellationToken).ConfigureAwait(false);
        return response.Status == SpfDnsStatus.Success
            ? SpfDnsResponse<SpfMxHost>.Success(
                response.Records
                    .OrderBy(static record => record.Preference)
                    .ThenBy(static record => record.Exchange, StringComparer.OrdinalIgnoreCase)
                    .ToArray())
            : response;
    }

    public ValueTask<SpfDnsResponse<string>> QueryPtrAsync(
        IPAddress address,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        return QueryAsync(BuildReverseLookupName(address), TypePtr, ReadPtrRecord, cancellationToken);
    }

    private async ValueTask<SpfDnsResponse<T>> QueryAsync<T>(
        string domain,
        ushort queryType,
        Func<byte[], int, int, T?> readRecord,
        CancellationToken cancellationToken)
    {
        if (_nameServers.Count == 0)
        {
            return SpfDnsResponse<T>.TemporaryError();
        }

        DnsQuery query;
        try
        {
            query = BuildQuery(NormalizeDomain(domain), queryType);
        }
        catch (ArgumentException)
        {
            return SpfDnsResponse<T>.NoData();
        }
        catch (InvalidOperationException)
        {
            return SpfDnsResponse<T>.NoData();
        }

        var sawTemporaryError = false;
        foreach (var server in _nameServers)
        {
            try
            {
                var response = await _exchangeAsync(server, query.Buffer, cancellationToken).ConfigureAwait(false);
                var parsed = ParseResponse(response, query.TransactionId, queryType, readRecord);
                if (parsed.Status == SpfDnsStatus.TemporaryError)
                {
                    sawTemporaryError = true;
                    continue;
                }

                return parsed;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (IOException)
            {
                sawTemporaryError = true;
            }
            catch (SocketException)
            {
                sawTemporaryError = true;
            }
            catch (TimeoutException)
            {
                sawTemporaryError = true;
            }
        }

        return sawTemporaryError
            ? SpfDnsResponse<T>.TemporaryError()
            : SpfDnsResponse<T>.NoData();
    }

    private static async ValueTask<byte[]> ExchangeUdpAsync(
        IPEndPoint server,
        byte[] query,
        CancellationToken cancellationToken)
    {
        using var udp = new UdpClient(server.AddressFamily);
        using var timeout = new CancellationTokenSource(QueryTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        await udp.SendAsync(query, query.Length, server)
            .WaitAsync(linked.Token)
            .ConfigureAwait(false);
        var response = await udp.ReceiveAsync().WaitAsync(linked.Token).ConfigureAwait(false);
        return response.Buffer;
    }

    private static DnsQuery BuildQuery(string domainName, ushort queryType)
    {
        if (domainName.Length == 0)
        {
            throw new ArgumentException("DNS query name cannot be empty.", nameof(domainName));
        }

        var transactionId = (ushort)RandomNumberGenerator.GetInt32(ushort.MaxValue + 1);
        var buffer = new List<byte>(512);
        Span<byte> header = stackalloc byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(header[0..2], transactionId);
        BinaryPrimitives.WriteUInt16BigEndian(header[2..4], QueryFlags);
        BinaryPrimitives.WriteUInt16BigEndian(header[4..6], 1);
        buffer.AddRange(header.ToArray());
        WriteName(buffer, domainName);

        Span<byte> question = stackalloc byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(question[0..2], queryType);
        BinaryPrimitives.WriteUInt16BigEndian(question[2..4], ClassInternet);
        buffer.AddRange(question.ToArray());

        return new DnsQuery(transactionId, buffer.ToArray());
    }

    private static SpfDnsResponse<T> ParseResponse<T>(
        byte[] response,
        ushort expectedTransactionId,
        ushort expectedType,
        Func<byte[], int, int, T?> readRecord)
    {
        if (response.Length < 12
            || BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(0, 2)) != expectedTransactionId)
        {
            return SpfDnsResponse<T>.TemporaryError();
        }

        var flags = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(2, 2));
        if ((flags & TruncatedFlag) != 0)
        {
            return SpfDnsResponse<T>.TemporaryError();
        }

        var responseCode = (ushort)(flags & ResponseCodeMask);
        if (responseCode == ResponseCodeNameError)
        {
            return SpfDnsResponse<T>.NameError();
        }

        if (responseCode != ResponseCodeNoError)
        {
            return SpfDnsResponse<T>.TemporaryError();
        }

        var questionCount = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(4, 2));
        var answerCount = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(6, 2));
        var offset = 12;
        for (var i = 0; i < questionCount; i++)
        {
            if (!TryReadName(response, ref offset, out _) || offset + 4 > response.Length)
            {
                return SpfDnsResponse<T>.TemporaryError();
            }

            offset += 4;
        }

        var records = new List<T>();
        for (var i = 0; i < answerCount && offset < response.Length; i++)
        {
            if (!TryReadName(response, ref offset, out _) || offset + 10 > response.Length)
            {
                return SpfDnsResponse<T>.TemporaryError();
            }

            var type = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(offset, 2));
            var recordClass = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(offset + 2, 2));
            var dataLength = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(offset + 8, 2));
            offset += 10;
            if (offset + dataLength > response.Length)
            {
                return SpfDnsResponse<T>.TemporaryError();
            }

            if (type == expectedType && recordClass == ClassInternet)
            {
                var record = readRecord(response, offset, dataLength);
                if (record is not null)
                {
                    records.Add(record);
                }
            }

            offset += dataLength;
        }

        return records.Count == 0
            ? SpfDnsResponse<T>.NoData()
            : SpfDnsResponse<T>.Success(records.ToArray());
    }

    private static string? ReadTxtRecord(byte[] message, int offset, int length)
    {
        var end = offset + length;
        var output = new StringBuilder(length);
        while (offset < end)
        {
            var chunkLength = message[offset++];
            if (offset + chunkLength > end)
            {
                return null;
            }

            output.Append(Encoding.ASCII.GetString(message, offset, chunkLength));
            offset += chunkLength;
        }

        return output.ToString();
    }

    private static IPAddress? ReadAddressRecord(byte[] message, int offset, int length)
    {
        if (length is not 4 and not 16)
        {
            return null;
        }

        return new IPAddress(message.AsSpan(offset, length));
    }

    private static SpfMxHost? ReadMxRecord(byte[] message, int offset, int length)
    {
        if (length < 3)
        {
            return null;
        }

        var end = offset + length;
        var preference = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(offset, 2));
        offset += 2;
        if (!TryReadName(message, ref offset, out var exchange) || offset > end || exchange.Length == 0)
        {
            return null;
        }

        return new SpfMxHost(NormalizeRecordName(exchange), preference);
    }

    private static string? ReadPtrRecord(byte[] message, int offset, int length)
    {
        var end = offset + length;
        return TryReadName(message, ref offset, out var name) && offset <= end && name.Length > 0
            ? NormalizeRecordName(name)
            : null;
    }

    private static void WriteName(List<byte> buffer, string domainName)
    {
        foreach (var label in domainName.TrimEnd('.').Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            if (bytes.Length is 0 or > 63)
            {
                throw new InvalidOperationException("DNS label is empty or too long.");
            }

            buffer.Add((byte)bytes.Length);
            buffer.AddRange(bytes);
        }

        buffer.Add(0);
    }

    private static bool TryReadName(byte[] message, ref int offset, out string name)
    {
        var labels = new List<string>();
        var cursor = offset;
        var nextOffset = offset;
        var jumped = false;
        var jumps = 0;

        while (cursor < message.Length)
        {
            var length = message[cursor++];
            if (length == 0)
            {
                if (!jumped)
                {
                    nextOffset = cursor;
                }

                offset = nextOffset;
                name = string.Join('.', labels);
                return true;
            }

            if ((length & 0xC0) == 0xC0)
            {
                if (cursor >= message.Length || jumps++ > 16)
                {
                    break;
                }

                var pointer = ((length & 0x3F) << 8) | message[cursor++];
                if (pointer >= message.Length)
                {
                    break;
                }

                if (!jumped)
                {
                    nextOffset = cursor;
                }

                cursor = pointer;
                jumped = true;
                continue;
            }

            if ((length & 0xC0) != 0 || length > 63 || cursor + length > message.Length)
            {
                break;
            }

            labels.Add(Encoding.ASCII.GetString(message, cursor, length));
            cursor += length;
        }

        name = string.Empty;
        return false;
    }

    private static string BuildReverseLookupName(IPAddress address)
    {
        var normalized = address.IsIPv4MappedToIPv6
            ? address.MapToIPv4()
            : address;
        var bytes = normalized.GetAddressBytes();
        if (normalized.AddressFamily == AddressFamily.InterNetwork)
        {
            return string.Join('.', bytes.Reverse()) + ".in-addr.arpa";
        }

        if (normalized.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var nibbles = new List<char>(bytes.Length * 2);
            for (var index = bytes.Length - 1; index >= 0; index--)
            {
                var value = bytes[index];
                nibbles.Add(ToHex(value & 0x0F));
                nibbles.Add(ToHex(value >> 4));
            }

            return string.Join('.', nibbles) + ".ip6.arpa";
        }

        throw new ArgumentException("Unsupported address family.", nameof(address));
    }

    private static char ToHex(int value) =>
        (char)(value < 10 ? '0' + value : 'a' + value - 10);

    private static string NormalizeDomain(string value)
    {
        var trimmed = value.Trim().TrimEnd('.');
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return new IdnMapping().GetAscii(trimmed).TrimEnd('.').ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }

    private static string NormalizeRecordName(string value) =>
        value.Trim().TrimEnd('.').ToLowerInvariant();

    private static IReadOnlyList<IPEndPoint> GetSystemNameServerEndpoints()
    {
        var endpoints = new List<IPEndPoint>();
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up
                || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var address in networkInterface.GetIPProperties().DnsAddresses)
            {
                if (!IPAddress.IsLoopback(address))
                {
                    endpoints.Add(new IPEndPoint(address, DnsPort));
                }
            }
        }

        return endpoints.Distinct().ToArray();
    }

    private sealed record DnsQuery(
        ushort TransactionId,
        byte[] Buffer);
}
