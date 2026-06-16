using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace HMailServer.Delivery;

public sealed class SystemDnsMxResolver : IDnsMxResolver
{
    private const int DnsPort = 53;
    private const ushort QueryFlags = 0x0100;
    private const ushort TypeMx = 15;
    private const ushort ClassInternet = 1;
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

        foreach (var server in _nameServers)
        {
            try
            {
                var records = await QueryServerAsync(server, domainName, cancellationToken).ConfigureAwait(false);
                if (records.Count > 0)
                {
                    return records;
                }
            }
            catch (IOException)
            {
            }
            catch (SocketException)
            {
            }
            catch (TimeoutException)
            {
            }
        }

        return Array.Empty<DnsMxRecord>();
    }

    private static async ValueTask<IReadOnlyList<DnsMxRecord>> QueryServerAsync(
        IPAddress server,
        string domainName,
        CancellationToken cancellationToken)
    {
        var query = BuildQuery(domainName);
        using var udp = new UdpClient(server.AddressFamily);
        using var timeout = new CancellationTokenSource(QueryTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        await udp.SendAsync(query.Buffer, query.Buffer.Length, new IPEndPoint(server, DnsPort))
            .WaitAsync(linked.Token)
            .ConfigureAwait(false);
        var response = await udp.ReceiveAsync().WaitAsync(linked.Token).ConfigureAwait(false);
        return ParseMxResponse(response.Buffer, query.TransactionId);
    }

    private static DnsQuery BuildQuery(string domainName)
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
        BinaryPrimitives.WriteUInt16BigEndian(question[0..2], TypeMx);
        BinaryPrimitives.WriteUInt16BigEndian(question[2..4], ClassInternet);
        buffer.AddRange(question.ToArray());

        return new DnsQuery(transactionId, buffer.ToArray());
    }

    private static IReadOnlyList<DnsMxRecord> ParseMxResponse(
        byte[] response,
        ushort expectedTransactionId)
    {
        if (response.Length < 12 ||
            BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(0, 2)) != expectedTransactionId)
        {
            return Array.Empty<DnsMxRecord>();
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

        return string.Join('.', labels);
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
