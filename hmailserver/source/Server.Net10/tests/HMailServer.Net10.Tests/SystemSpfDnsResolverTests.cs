using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SystemSpfDnsResolverTests
{
    private const ushort TypeA = 1;
    private const ushort TypePtr = 12;
    private const ushort TypeMx = 15;
    private const ushort TypeTxt = 16;
    private const ushort ClassInternet = 1;

    [TestMethod]
    public async Task QueryTxtAsync_ParsesConcatenatedTxtChunks()
    {
        string? queriedName = null;
        ushort queriedType = 0;
        var resolver = CreateResolver(
            query =>
            {
                var id = ReadTransactionId(query);
                queriedName = ReadQuestionName(query, out queriedType);
                return BuildResponse(
                    id,
                    queriedName,
                    queriedType,
                    new ResourceRecord(TypeTxt, BuildTxt("v=spf1 ", "-all")));
            });

        var response = await resolver.QueryTxtAsync("Example.Test.", CancellationToken.None);

        Assert.AreEqual(SpfDnsStatus.Success, response.Status);
        CollectionAssert.AreEqual(new[] { "v=spf1 -all" }, response.Records.ToArray());
        Assert.AreEqual("example.test", queriedName);
        Assert.AreEqual(TypeTxt, queriedType);
    }

    [TestMethod]
    public async Task QueryMxAsync_ParsesAndSortsMxHosts()
    {
        var resolver = CreateResolver(
            query =>
            {
                var id = ReadTransactionId(query);
                var question = ReadQuestionName(query, out var type);
                return BuildResponse(
                    id,
                    question,
                    type,
                    new ResourceRecord(TypeMx, BuildMx(20, "mx20.example.test")),
                    new ResourceRecord(TypeMx, BuildMx(10, "mx10.example.test")));
            });

        var response = await resolver.QueryMxAsync("example.test", CancellationToken.None);

        Assert.AreEqual(SpfDnsStatus.Success, response.Status);
        Assert.AreEqual("mx10.example.test", response.Records[0].Exchange);
        Assert.AreEqual(10, response.Records[0].Preference);
        Assert.AreEqual("mx20.example.test", response.Records[1].Exchange);
    }

    [TestMethod]
    public async Task QueryAddressesAsync_ParsesARecords()
    {
        var resolver = CreateResolver(
            query =>
            {
                var id = ReadTransactionId(query);
                var question = ReadQuestionName(query, out var type);
                return BuildResponse(
                    id,
                    question,
                    type,
                    new ResourceRecord(TypeA, IPAddress.Parse("192.0.2.5").GetAddressBytes()));
            });

        var response = await resolver.QueryAddressesAsync(
            "example.test",
            AddressFamily.InterNetwork,
            CancellationToken.None);

        Assert.AreEqual(SpfDnsStatus.Success, response.Status);
        CollectionAssert.AreEqual(new[] { IPAddress.Parse("192.0.2.5") }, response.Records.ToArray());
    }

    [TestMethod]
    public async Task QueryPtrAsync_UsesReverseLookupNameAndParsesPtrRecords()
    {
        string? queriedName = null;
        var resolver = CreateResolver(
            query =>
            {
                var id = ReadTransactionId(query);
                queriedName = ReadQuestionName(query, out var type);
                return BuildResponse(
                    id,
                    queriedName,
                    type,
                    new ResourceRecord(TypePtr, EncodeName("mail.example.test")));
            });

        var response = await resolver.QueryPtrAsync(
            IPAddress.Parse("192.0.2.5"),
            CancellationToken.None);

        Assert.AreEqual("5.2.0.192.in-addr.arpa", queriedName);
        Assert.AreEqual(SpfDnsStatus.Success, response.Status);
        CollectionAssert.AreEqual(new[] { "mail.example.test" }, response.Records.ToArray());
    }

    [TestMethod]
    public async Task QueryTxtAsync_MapsNoDataNameErrorAndTemporaryErrors()
    {
        var noData = CreateResolver(
            query =>
            {
                var id = ReadTransactionId(query);
                var question = ReadQuestionName(query, out var type);
                return BuildResponse(id, question, type);
            });
        var nameError = CreateResolver(
            query =>
            {
                var id = ReadTransactionId(query);
                var question = ReadQuestionName(query, out var type);
                return BuildResponse(id, question, type, responseCode: 3);
            });
        var temporary = CreateResolver(
            query =>
            {
                var id = ReadTransactionId(query);
                var question = ReadQuestionName(query, out var type);
                return BuildResponse(id, question, type, responseCode: 2);
            });

        Assert.AreEqual(
            SpfDnsStatus.NoData,
            (await noData.QueryTxtAsync("example.test", CancellationToken.None)).Status);
        Assert.AreEqual(
            SpfDnsStatus.NameError,
            (await nameError.QueryTxtAsync("example.test", CancellationToken.None)).Status);
        Assert.AreEqual(
            SpfDnsStatus.TemporaryError,
            (await temporary.QueryTxtAsync("example.test", CancellationToken.None)).Status);
    }

    private static SystemSpfDnsResolver CreateResolver(Func<byte[], byte[]> exchange) =>
        new(
            [new IPEndPoint(IPAddress.Loopback, 53)],
            (_, query, _) => ValueTask.FromResult(exchange(query)));

    private static ushort ReadTransactionId(byte[] query) =>
        BinaryPrimitives.ReadUInt16BigEndian(query.AsSpan(0, 2));

    private static string ReadQuestionName(byte[] query, out ushort type)
    {
        var offset = 12;
        var labels = new List<string>();
        while (query[offset] != 0)
        {
            var length = query[offset++];
            labels.Add(Encoding.ASCII.GetString(query, offset, length));
            offset += length;
        }

        offset++;
        type = BinaryPrimitives.ReadUInt16BigEndian(query.AsSpan(offset, 2));
        return string.Join('.', labels);
    }

    private static byte[] BuildResponse(
        ushort id,
        string questionName,
        ushort questionType,
        params ResourceRecord[] records) =>
        BuildResponse(id, questionName, questionType, responseCode: 0, records);

    private static byte[] BuildResponse(
        ushort id,
        string questionName,
        ushort questionType,
        int responseCode,
        params ResourceRecord[] records)
    {
        var buffer = new List<byte>(512);
        Span<byte> header = stackalloc byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(header[0..2], id);
        BinaryPrimitives.WriteUInt16BigEndian(header[2..4], (ushort)(0x8180 | responseCode));
        BinaryPrimitives.WriteUInt16BigEndian(header[4..6], 1);
        BinaryPrimitives.WriteUInt16BigEndian(header[6..8], (ushort)records.Length);
        buffer.AddRange(header.ToArray());
        buffer.AddRange(EncodeName(questionName));
        WriteUInt16(buffer, questionType);
        WriteUInt16(buffer, ClassInternet);

        foreach (var record in records)
        {
            buffer.Add(0xC0);
            buffer.Add(0x0C);
            WriteUInt16(buffer, record.Type);
            WriteUInt16(buffer, ClassInternet);
            WriteUInt32(buffer, 60);
            WriteUInt16(buffer, (ushort)record.Data.Length);
            buffer.AddRange(record.Data);
        }

        return buffer.ToArray();
    }

    private static byte[] BuildTxt(params string[] chunks)
    {
        var buffer = new List<byte>();
        foreach (var chunk in chunks)
        {
            var bytes = Encoding.ASCII.GetBytes(chunk);
            buffer.Add((byte)bytes.Length);
            buffer.AddRange(bytes);
        }

        return buffer.ToArray();
    }

    private static byte[] BuildMx(ushort preference, string exchange)
    {
        var buffer = new List<byte>();
        WriteUInt16(buffer, preference);
        buffer.AddRange(EncodeName(exchange));
        return buffer.ToArray();
    }

    private static byte[] EncodeName(string name)
    {
        var buffer = new List<byte>();
        foreach (var label in name.Split('.'))
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            buffer.Add((byte)bytes.Length);
            buffer.AddRange(bytes);
        }

        buffer.Add(0);
        return buffer.ToArray();
    }

    private static void WriteUInt16(List<byte> buffer, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        buffer.AddRange(bytes.ToArray());
    }

    private static void WriteUInt32(List<byte> buffer, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        buffer.AddRange(bytes.ToArray());
    }

    private sealed record ResourceRecord(
        ushort Type,
        byte[] Data);
}
