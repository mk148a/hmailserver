using System.Buffers.Binary;
using System.Text;
using HMailServer.Delivery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SystemDnsMxResolverTests
{
    [TestMethod]
    public void ParseMxResponse_PreservesNullMxRootExchange()
    {
        var records = ParseMxResponse(BuildMxResponse(0x1234, 0, [0]), 0x1234);

        Assert.AreEqual(1, records.Count);
        Assert.AreEqual(".", records[0].Exchange);
        Assert.AreEqual((ushort)0, records[0].Preference);
    }

    [TestMethod]
    public void ParseMxResponse_PreservesOrdinaryMxExchange()
    {
        var records = ParseMxResponse(
            BuildMxResponse(0x1234, 10, EncodeName("mx.example.net")),
            0x1234);

        Assert.AreEqual(1, records.Count);
        Assert.AreEqual("mx.example.net", records[0].Exchange);
        Assert.AreEqual((ushort)10, records[0].Preference);
    }

    [TestMethod]
    public void ParseCnameResponse_PreservesTargetAndTtl()
    {
        var records = ParseCnameResponse(
            BuildCnameResponse(0x1234, EncodeName("target.example.net")),
            0x1234);

        Assert.AreEqual(1, records.Count);
        Assert.AreEqual("target.example.net", records[0].Target);
        Assert.AreEqual(TimeSpan.FromSeconds(300), records[0].TimeToLive);
    }

    private static IReadOnlyList<DnsMxRecord> ParseMxResponse(byte[] response, ushort transactionId)
    {
        var method = typeof(SystemDnsMxResolver).GetMethod(
            "ParseMxResponse",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(method);
        return (IReadOnlyList<DnsMxRecord>)method.Invoke(null, [response, transactionId])!;
    }

    private static IReadOnlyList<DnsCnameRecord> ParseCnameResponse(byte[] response, ushort transactionId)
    {
        var method = typeof(SystemDnsMxResolver).GetMethod(
            "ParseCnameResponse",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(method);
        return (IReadOnlyList<DnsCnameRecord>)method.Invoke(null, [response, transactionId])!;
    }

    private static byte[] BuildMxResponse(ushort transactionId, ushort preference, byte[] exchange)
    {
        var response = new List<byte>(64);
        Span<byte> header = stackalloc byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(header[0..2], transactionId);
        BinaryPrimitives.WriteUInt16BigEndian(header[2..4], 0x8180);
        BinaryPrimitives.WriteUInt16BigEndian(header[4..6], 1);
        BinaryPrimitives.WriteUInt16BigEndian(header[6..8], 1);
        response.AddRange(header.ToArray());

        response.Add(7);
        response.AddRange(Encoding.ASCII.GetBytes("example"));
        response.Add(3);
        response.AddRange(Encoding.ASCII.GetBytes("net"));
        response.Add(0);
        AddUInt16(response, 15);
        AddUInt16(response, 1);

        response.Add(0xC0);
        response.Add(0x0C);
        AddUInt16(response, 15);
        AddUInt16(response, 1);
        AddUInt32(response, 300);
        AddUInt16(response, checked((ushort)(2 + exchange.Length)));
        AddUInt16(response, preference);
        response.AddRange(exchange);
        return response.ToArray();
    }

    private static byte[] BuildCnameResponse(ushort transactionId, byte[] target)
    {
        var response = new List<byte>(64);
        Span<byte> header = stackalloc byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(header[0..2], transactionId);
        BinaryPrimitives.WriteUInt16BigEndian(header[2..4], 0x8180);
        BinaryPrimitives.WriteUInt16BigEndian(header[4..6], 1);
        BinaryPrimitives.WriteUInt16BigEndian(header[6..8], 1);
        response.AddRange(header.ToArray());

        response.Add(7);
        response.AddRange(Encoding.ASCII.GetBytes("example"));
        response.Add(3);
        response.AddRange(Encoding.ASCII.GetBytes("net"));
        response.Add(0);
        AddUInt16(response, 5);
        AddUInt16(response, 1);

        response.Add(0xC0);
        response.Add(0x0C);
        AddUInt16(response, 5);
        AddUInt16(response, 1);
        AddUInt32(response, 300);
        AddUInt16(response, checked((ushort)target.Length));
        response.AddRange(target);
        return response.ToArray();
    }

    private static byte[] EncodeName(string name)
    {
        var encoded = new List<byte>();
        foreach (var label in name.Split('.'))
        {
            encoded.Add((byte)label.Length);
            encoded.AddRange(Encoding.ASCII.GetBytes(label));
        }

        encoded.Add(0);
        return encoded.ToArray();
    }

    private static void AddUInt16(List<byte> buffer, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        buffer.AddRange(bytes.ToArray());
    }

    private static void AddUInt32(List<byte> buffer, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        buffer.AddRange(bytes.ToArray());
    }
}
