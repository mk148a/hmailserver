using System.Text;
using HMailServer.Protocols;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ProtocolPipelineTests
{
    [TestMethod]
    public async Task ReadLineAsync_ReadsCrlfDelimitedLines()
    {
        await using var stream = new MemoryStream(Encoding.ASCII.GetBytes("NOOP\r\nCAPABILITY\r\n"));
        await using var reader = new LineProtocolReader(stream, maxLineBytes: 64);

        Assert.AreEqual("NOOP", await reader.ReadLineAsync(CancellationToken.None));
        Assert.AreEqual("CAPABILITY", await reader.ReadLineAsync(CancellationToken.None));
        Assert.IsNull(await reader.ReadLineAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadLineAsync_RejectsLfOnlyLine()
    {
        await using var stream = new MemoryStream(Encoding.ASCII.GetBytes("NOOP\n"));
        await using var reader = new LineProtocolReader(stream, maxLineBytes: 64);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => reader.ReadLineAsync(CancellationToken.None).AsTask());
    }

    [TestMethod]
    public async Task ReadLineAsync_RejectsOverlongLine()
    {
        await using var stream = new MemoryStream(Encoding.ASCII.GetBytes("123456\r\n"));
        await using var reader = new LineProtocolReader(stream, maxLineBytes: 4);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => reader.ReadLineAsync(CancellationToken.None).AsTask());
    }

    [TestMethod]
    public async Task ReadExactAsync_ReadsLiteralAfterCommandLine()
    {
        await using var stream = new MemoryStream(Encoding.ASCII.GetBytes("APPEND INBOX {5}\r\nHello\r\nNOOP\r\n"));
        await using var reader = new LineProtocolReader(stream, maxLineBytes: 64);

        Assert.AreEqual("APPEND INBOX {5}", await reader.ReadLineAsync(CancellationToken.None));
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("Hello"), await reader.ReadExactAsync(5, CancellationToken.None));
        Assert.AreEqual(string.Empty, await reader.ReadLineAsync(CancellationToken.None));
        Assert.AreEqual("NOOP", await reader.ReadLineAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task BoundedWorkQueue_ReadsQueuedItems()
    {
        var queue = new BoundedWorkQueue<int>(capacity: 2);
        await queue.EnqueueAsync(10, CancellationToken.None);
        await queue.EnqueueAsync(20, CancellationToken.None);
        queue.TryComplete();

        var values = new List<int>();
        await foreach (var value in queue.ReadAllAsync(CancellationToken.None))
        {
            values.Add(value);
        }

        CollectionAssert.AreEqual(new[] { 10, 20 }, values);
    }
}
