using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace HMailServer.Delivery;

public sealed class SystemDnsMxResolver : IDnsMxResolver, IDnsCnameResolver
{
    private const int DnsPort = 53;
    private const ushort QueryFlags = 0x0100;
    private const ushort TypeMx = 15;
    private const ushort TypeCname = 5;
    private const ushort ClassInternet = 1;
    private const ushort ResponseCodeMask = 0x000F;
    private const ushort ResponseCodeNoError = 0;
    private const ushort ResponseCodeNameError = 3;
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(5);

    private readonly IReadOnlyList<IPAddress> _nameServers;

    public SystemDnsMxResolver()
        : this(GetSystemNameServers())
    {
    }

    public SystemDnsMxResolver(IReadOnlyList<IPAddress> nameServers)
    {
        _nameServers = nameServers;
    }

    public async ValueTask<IReadOnlyList<DnsMxRecord>> ResolveMxAsync(
        string domainName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domainName);

        if (_nameServers.Count == 0)
        {
            throw new IOException("No DNS name servers are configured for MX lookup.");
        }

        Exception? lastFailure = null;
        foreach (var server in _nameServers)
        {
            try
            {
                var query = BuildQuery(domainName, TypeMx);
                var response = await QueryServerAsync(server, query.Buffer, cancellationToken).ConfigureAwait(false);
                return ParseMxResponse(response, query.TransactionId);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastFailure = new TimeoutException("DNS MX lookup timed out.");
            }
            catch (IOException ex)
            {
                lastFailure = ex;
            }
            catch (SocketException ex)
            {
                lastFailure = ex;
            }
            catch (TimeoutException ex)
            {
                lastFailure = ex;
            }
        }

        if (lastFailure is not null)
        {
            throw new IOException("DNS MX lookup failed.", lastFailure);
        }

        return Array.Empty<DnsMxRecord>();
    }

    public async ValueTask<IReadOnlyList<DnsCnameRecord>> ResolveCnameAsync(
        string domainName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domainName);

        if (_nameServers.Count == 0)
        {
            throw new IOException("No DNS name servers are configured for CNAME lookup.");
        }

        Exception? lastFailure = null;
        foreach (var server in _nameServers)
        {
            try
            {
                var query = BuildQuery(domainName, TypeCname);
                var response = await QueryServerAsync(server, query.Buffer, cancellationToken).ConfigureAwait(false);
                return ParseCnameResponse(response, query.TransactionId);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastFailure = new TimeoutException("DNS CNAME lookup timed out.");
            }
            catch (IOException ex)
            {
                lastFailure = ex;
            }
            catch (SocketException ex)
            {
                lastFailure = ex;
            }
            catch (TimeoutException ex)
            {
                lastFailure = ex;
            }
        }

        if (lastFailure is not null)
        {
            throw new IOException("DNS CNAME lookup failed.", lastFailure);
        }

        return Array.Empty<DnsCnameRecord>();
    }

    private static async ValueTask<byte[]> QueryServerAsync(
        IPAddress server,
        byte[] query,
        CancellationToken cancellationToken)
    {
        using var udp = new UdpClient(server.AddressFamily);
        using var timeout = new CancellationTokenSource(QueryTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        await udp.SendAsync(query, query.Length, new IPEndPoint(server, DnsPort))
            .WaitAsync(linked.Token)
            .ConfigureAwait(false);
        var response = await udp.ReceiveAsync().WaitAsync(linked.Token).ConfigureAwait(false);
        return response.Buffer;
    }

    private static DnsQuery BuildQuery(string domainName, ushort queryType)
    {
        var transactionId = (ushort)RandomNumberGenerator.GetInt32(ushort.MaxValue + 1);
        var buffer = new List<byte>(512);
        Span<byte> header = stackalloc byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(header[0..2], transactionId);
        BinaryPrimitives.WriteUInt16BigEndian(header[2..4], QueryFlags);
        BinaryPrimitives.WriteUInt16BigEndian(header[4..6], 1);
        buffer.AddRange(header.ToArray());

        foreach (var label in domainName.TrimEnd('.').Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            if (bytes.Length > 63)
            {
                throw new InvalidOperationException("DNS label is too long.");
            }

            buffer.Add((byte)bytes.Length);
            buffer.AddRange(bytes);
        }

        buffer.Add(0);
        Span<byte> question = stackalloc byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(question[0..2], queryType);
        BinaryPrimitives.WriteUInt16BigEndian(question[2..4], ClassInternet);
        buffer.AddRange(question.ToArray());

        return new DnsQuery(transactionId, buffer.ToArray());
    }

    private static IReadOnlyList<DnsCnameRecord> ParseCnameResponse(
        byte[] response,
        ushort expectedTransactionId)
    {
        if (response.Length < 12 ||
            BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(0, 2)) != expectedTransactionId)
        {
            throw new IOException("DNS CNAME response did not match the query.");
        }

        var flags = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(2, 2));
        var responseCode = (ushort)(flags & ResponseCodeMask);
        if (responseCode == ResponseCodeNameError)
        {
            return Array.Empty<DnsCnameRecord>();
        }

        if (responseCode != ResponseCodeNoError)
        {
            throw new IOException("DNS CNAME lookup failed with response code " + responseCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
        }

        var questionCount = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(4, 2));
        var answerCount = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(6, 2));
        var offset = 12;
        for (var i = 0; i < questionCount; i++)
        {
            SkipName(response, ref offset);
            offset += 4;
        }

        var records = new List<DnsCnameRecord>();
        for (var i = 0; i < answerCount && offset < response.Length; i++)
        {
            SkipName(response, ref offset);
            if (offset + 10 > response.Length)
            {
                break;
            }

            var type = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(offset, 2));
            var klass = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(offset + 2, 2));
            var ttlSeconds = BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(offset + 4, 4));
            var dataLength = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(offset + 8, 2));
            offset += 10;
            if (offset + dataLength > response.Length)
            {
                break;
            }

            if (type == TypeCname && klass == ClassInternet)
            {
                var targetOffset = offset;
                var target = ReadName(response, ref targetOffset);
                if (!string.IsNullOrWhiteSpace(target))
                {
                    records.Add(new DnsCnameRecord(target, TimeSpan.FromSeconds(ttlSeconds)));
                }
            }

            offset += dataLength;
        }

        return records;
    }

    private static IReadOnlyList<DnsMxRecord> ParseMxResponse(
        byte[] response,
        ushort expectedTransactionId)
    {
        if (response.Length < 12 ||
            BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(0, 2)) != expectedTransactionId)
        {
            throw new IOException("DNS MX response did not match the query.");
        }

        var flags = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(2, 2));
        var responseCode = (ushort)(flags & ResponseCodeMask);
        if (responseCode == ResponseCodeNameError)
        {
            return Array.Empty<DnsMxRecord>();
        }

        if (responseCode != ResponseCodeNoError)
        {
            throw new IOException("DNS MX lookup failed with response code " + responseCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
        }

        var questionCount = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(4, 2));
        var answerCount = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(6, 2));
        var offset = 12;
        for (var i = 0; i < questionCount; i++)
        {
            SkipName(response, ref offset);
            offset += 4;
        }

        var records = new List<DnsMxRecord>();
        for (var i = 0; i < answerCount && offset < response.Length; i++)
        {
            SkipName(response, ref offset);
            if (offset + 10 > response.Length)
            {
                break;
            }

            var type = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(offset, 2));
            var klass = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(offset + 2, 2));
            var ttlSeconds = BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(offset + 4, 4));
            var dataLength = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(offset + 8, 2));
            offset += 10;
            if (offset + dataLength > response.Length)
            {
                break;
            }

            if (type == TypeMx && klass == ClassInternet && dataLength >= 3)
            {
                var preference = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(offset, 2));
                var exchangeOffset = offset + 2;
                var exchange = ReadName(response, ref exchangeOffset);
                if (!string.IsNullOrWhiteSpace(exchange))
                {
                    records.Add(new DnsMxRecord(exchange, preference, TimeSpan.FromSeconds(ttlSeconds)));
                }
            }

            offset += dataLength;
        }

        return records
            .OrderBy(static record => record.Preference)
            .ThenBy(static record => record.Exchange, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ReadName(byte[] message, ref int offset)
    {
        var labels = new List<string>();
        var jumped = false;
        var cursor = offset;
        var jumps = 0;

        while (cursor < message.Length)
        {
            var length = message[cursor++];
            if (length == 0)
            {
                break;
            }

            if ((length & 0xC0) == 0xC0)
            {
                if (cursor >= message.Length || jumps++ > 16)
                {
                    break;
                }

                var pointer = ((length & 0x3F) << 8) | message[cursor++];
                if (!jumped)
                {
                    offset = cursor;
                }

                cursor = pointer;
                jumped = true;
                continue;
            }

            if (cursor + length > message.Length)
            {
                break;
            }

            labels.Add(Encoding.ASCII.GetString(message, cursor, length));
            cursor += length;
        }

        if (!jumped)
        {
            offset = cursor;
        }

        return labels.Count == 0 ? "." : string.Join('.', labels);
    }

    private static void SkipName(byte[] message, ref int offset)
    {
        _ = ReadName(message, ref offset);
    }

    private static IReadOnlyList<IPAddress> GetSystemNameServers()
    {
        var addresses = new List<IPAddress>();
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var address in networkInterface.GetIPProperties().DnsAddresses)
            {
                if (!IPAddress.IsLoopback(address))
                {
                    addresses.Add(address);
                }
            }
        }

        return addresses.Distinct().ToArray();
    }

    private sealed record DnsQuery(
        ushort TransactionId,
        byte[] Buffer);
}
